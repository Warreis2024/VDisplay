using System.IO.Pipes;
using System.Text.Json;
using VDisplay.Core;
using VDisplay.Core.Models;
using VDisplay.Service.Capture;
using VDisplay.Service.Compositor;
using VDisplay.Service.Driver;
using VDisplay.Service.Layout;
using VDisplay.Service.Monitor;

namespace VDisplay.Service.Ipc;

public sealed class IpcHostedService : BackgroundService
{
    private readonly ILogger<IpcHostedService> _logger;
    private readonly DriverInstaller _driverInstaller;
    private readonly MonitorManager _monitorManager;
    private readonly CaptureHostedService _captureService;
    private readonly PhysicalCompositorService _compositorService;
    private readonly PhysicalMonitorProvider _physicalMonitorProvider;

    public IpcHostedService(
        ILogger<IpcHostedService> logger,
        DriverInstaller driverInstaller,
        MonitorManager monitorManager,
        CaptureHostedService captureService,
        PhysicalCompositorService compositorService,
        PhysicalMonitorProvider physicalMonitorProvider)
    {
        _logger = logger;
        _driverInstaller = driverInstaller;
        _monitorManager = monitorManager;
        _captureService = captureService;
        _compositorService = compositorService;
        _physicalMonitorProvider = physicalMonitorProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VDisplay IPC sunucusu başlatılıyor...");

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                IpcConstants.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPC bağlantısı işlenirken hata oluştu.");
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var request = await IpcSerializer.ReadAsync<IpcRequest>(pipe, cancellationToken);
        if (request is null)
        {
            await IpcSerializer.WriteAsync(pipe, IpcResponse.Fail("Geçersiz istek."), cancellationToken);
            return;
        }

        var response = await ProcessRequestAsync(request, cancellationToken);
        await IpcSerializer.WriteAsync(pipe, response, cancellationToken);
    }

    private Task<IpcResponse> ProcessRequestAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(request.Command switch
            {
                IpcCommand.Ping => IpcResponse.Ok(message: "pong"),
                IpcCommand.GetStatus => IpcResponse.Ok(
                    JsonSerializer.Serialize(BuildStatus())),
                IpcCommand.StartDriver => StartDriver(),
                IpcCommand.StopDriver => StopDriver(),
                IpcCommand.SetMonitorCount => SetMonitorCount(request.Payload),
                IpcCommand.GetMonitors => IpcResponse.Ok(
                    JsonSerializer.Serialize(BuildStatus().Monitors)),
                IpcCommand.SetLayout => SetLayout(request.Payload),
                IpcCommand.GetPhysicalMonitors => IpcResponse.Ok(
                    JsonSerializer.Serialize(_physicalMonitorProvider.GetCaptureSources())),
                IpcCommand.GetVirtualMonitors => IpcResponse.Ok(
                    JsonSerializer.Serialize(_physicalMonitorProvider.GetVirtualMonitorDisplays(8))),
                IpcCommand.SetSourceMonitor => SetSourceMonitor(request.Payload),
                IpcCommand.StartCapture => StartCapture(),
                IpcCommand.StopCapture => StopCapture(),
                IpcCommand.StartVmSplit => StartVmSplit(request.Payload),
                IpcCommand.StopVmSplit => StopVmSplit(),
                IpcCommand.StartPhysicalSplit => StartVmSplit(request.Payload),
                IpcCommand.StopPhysicalSplit => StopVmSplit(),
                _ => IpcResponse.Fail("Bilinmeyen komut.")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Komut işlenirken hata: {Command}", request.Command);
            return Task.FromResult(IpcResponse.Fail(ex.Message));
        }
    }

    private ServiceStatus BuildStatus() =>
        _monitorManager.GetStatus(
            _driverInstaller.IsRunning,
            _captureService.IsRunning,
            _compositorService.IsRunning,
            (ulong)_captureService.FramesCaptured,
            (ulong)_compositorService.FramesRendered);

    private IpcResponse StartDriver()
    {
        if (_monitorManager.MonitorCount == 0)
        {
            _monitorManager.SetMonitorCount(2);
        }

        var started = _driverInstaller.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (started)
        {
            _physicalMonitorProvider.RefreshDeviceCache();
        }

        return started
            ? IpcResponse.Ok(message: "Sürücü başlatıldı.")
            : IpcResponse.Fail("Sürücü başlatılamadı. Test imzalama ve kurulumu kontrol edin.");
    }

    private IpcResponse StopDriver()
    {
        _captureService.StopCapture();
        _compositorService.Stop();
        _driverInstaller.Stop();
        return IpcResponse.Ok(message: "Sürücü durduruldu.");
    }

    private IpcResponse SetMonitorCount(string? payload)
    {
        var count = MonitorManager.ParseMonitorCount(payload);
        if (count is null)
        {
            return IpcResponse.Fail("Geçersiz monitör sayısı.");
        }

        _monitorManager.SetMonitorCount(count.Value);
        return IpcResponse.Ok(JsonSerializer.Serialize(BuildStatus()), $"Monitör sayısı {count} olarak ayarlandı.");
    }

    private IpcResponse SetLayout(string? payload)
    {
        var layout = LayoutManager.ParseLayoutType(payload);
        if (layout is null)
        {
            return IpcResponse.Fail("Geçersiz düzen.");
        }

        _monitorManager.SetLayout(layout.Value);
        return IpcResponse.Ok(JsonSerializer.Serialize(BuildStatus()), $"Düzen {layout} olarak ayarlandı.");
    }

    private IpcResponse SetSourceMonitor(string? payload)
    {
        var index = LayoutManager.ParseSourceMonitorIndex(payload);
        if (index is null)
        {
            return IpcResponse.Fail("Geçersiz kaynak monitör.");
        }

        _monitorManager.SetSourceMonitorIndex(index.Value);
        return IpcResponse.Ok(JsonSerializer.Serialize(BuildStatus()), $"Kaynak monitör {index} seçildi.");
    }

    private IpcResponse StartCapture()
    {
        if (!_driverInstaller.IsRunning)
        {
            return IpcResponse.Fail("Önce sürücüyü başlatın.");
        }

        _captureService.StartCapture();
        return IpcResponse.Ok(JsonSerializer.Serialize(BuildStatus()), "Capture başlatıldı.");
    }

    private IpcResponse StopCapture()
    {
        _captureService.StopCapture();
        return IpcResponse.Ok(JsonSerializer.Serialize(BuildStatus()), "Capture durduruldu.");
    }

    private IpcResponse StartVmSplit(string? payload)
    {
        if (!_driverInstaller.IsRunning)
        {
            return IpcResponse.Fail("Once surucuyu baslatin: vdisplay driver start");
        }

        var mode = ParseVmSplitMode(payload);
        var physicals = _physicalMonitorProvider.GetCaptureSources()
            .OrderBy(m => m.X)
            .ThenBy(m => m.Y)
            .ToList();

        if (physicals.Count == 0)
        {
            return IpcResponse.Fail("Fiziksel monitör bulunamadi.");
        }

        if (mode == VmSplitMode.Dual && physicals.Count >= 2)
        {
            _monitorManager.ApplyDualPhysicalSplit(physicals.Take(2).ToList());
        }
        else
        {
            var source = physicals.FirstOrDefault(m => m.IsPrimary) ?? physicals[0];
            _monitorManager.ApplyPrimarySplit(source);
            if (mode == VmSplitMode.Dual)
            {
                mode = VmSplitMode.Primary;
            }
        }

        _captureService.StartCapture();
        _physicalMonitorProvider.RefreshDeviceCache();

        var vmCount = _monitorManager.MonitorCount;
        var vms = _physicalMonitorProvider.GetVirtualMonitorDisplays(vmCount);
        var modeLabel = mode == VmSplitMode.Dual ? "dual (4 VM)" : "primary (2 VM)";
        var setupHint = BuildVmSplitSetupHint(mode, physicals, vms);

        return IpcResponse.Ok(
            JsonSerializer.Serialize(BuildStatus()),
            $"VM split aktif: {modeLabel}. {setupHint}");
    }

    private static string BuildVmSplitSetupHint(
        VmSplitMode mode,
        IReadOnlyList<PhysicalMonitorInfo> physicals,
        IReadOnlyList<PhysicalMonitorInfo> vms)
    {
        var lines = new List<string>
        {
            "Windows Ayarlar > Sistem > Ekran: VM'leri fiziksel panellerin yanina surukle.",
            "Her VM ayri monitör; pencere surukle, Win+Ok, meeting paylasimi calisir."
        };

        if (mode == VmSplitMode.Dual && physicals.Count >= 2)
        {
            lines.Add($"Fiziksel 1 ({physicals[0].Name}) -> VM1 sol, VM2 sag.");
            lines.Add($"Fiziksel 2 ({physicals[1].Name}) -> VM3 sol, VM4 sag.");
        }
        else if (physicals.Count > 0)
        {
            lines.Add($"Kaynak: {physicals[0].Name} -> VM1 sol, VM2 sag.");
        }

        if (vms.Count > 0)
        {
            lines.Add("Sanal: " + string.Join(", ", vms.Select(v => $"{v.Name} {v.Width}x{v.Height}")));
        }

        return string.Join(" ", lines);
    }

    private static VmSplitMode ParseVmSplitMode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return VmSplitMode.Dual;
        }

        return payload.Trim().ToLowerInvariant() switch
        {
            "primary" or "single" or "1" => VmSplitMode.Primary,
            "dual" or "all" or "2" => VmSplitMode.Dual,
            "hybrid" => VmSplitMode.Primary,
            _ => Enum.TryParse<VmSplitMode>(payload, true, out var parsed)
                ? parsed
                : VmSplitMode.Dual
        };
    }

    private IpcResponse StopVmSplit()
    {
        _captureService.StopCapture();
        _compositorService.Stop();
        return IpcResponse.Ok(JsonSerializer.Serialize(BuildStatus()), "VM split durduruldu (capture kapali).");
    }
}

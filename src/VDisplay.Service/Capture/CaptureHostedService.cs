using System.Runtime.Versioning;
using VDisplay.Service.Capture;
using VDisplay.Service.Driver;
using VDisplay.Service.Layout;

namespace VDisplay.Service.Capture;

[SupportedOSPlatform("windows")]
public sealed class CaptureHostedService : BackgroundService
{
    private readonly ILogger<CaptureHostedService> _logger;
    private readonly CaptureEngine _captureEngine;
    private readonly SharedFrameBridge _sharedFrameBridge;
    private readonly LayoutManager _layoutManager;
    private readonly Monitor.MonitorManager _monitorManager;
    private readonly DriverInstaller _driverInstaller;

    private volatile bool _captureRequested;
    private long _framesCaptured;

    public CaptureHostedService(
        ILogger<CaptureHostedService> logger,
        CaptureEngine captureEngine,
        SharedFrameBridge sharedFrameBridge,
        LayoutManager layoutManager,
        Monitor.MonitorManager monitorManager,
        DriverInstaller driverInstaller)
    {
        _logger = logger;
        _captureEngine = captureEngine;
        _sharedFrameBridge = sharedFrameBridge;
        _layoutManager = layoutManager;
        _monitorManager = monitorManager;
        _driverInstaller = driverInstaller;
    }

    public bool IsRunning => _captureRequested;

    public long FramesCaptured => Interlocked.Read(ref _framesCaptured);

    public void StartCapture()
    {
        _captureRequested = true;
    }

    public void StopCapture()
    {
        _captureRequested = false;
        _sharedFrameBridge.SetCaptureActive(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield first so other hosted services (IPC) can start even if MMF setup is slow.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_captureRequested || !_driverInstaller.IsRunning)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            try
            {
                _sharedFrameBridge.EnsureCreated();
                _monitorManager.RefreshVirtualMonitorMapping();
                var layout = _monitorManager.GetActiveLayout();
                if (layout is null || layout.Regions.Count == 0)
                {
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                _sharedFrameBridge.WriteLayout(layout, captureActive: true);

                var sourceGroups = layout.Regions
                    .GroupBy(r => r.SourceMonitorIndex)
                    .ToList();

                var capturedAny = false;
                foreach (var group in sourceGroups)
                {
                    _captureEngine.SetSourceMonitor(group.Key);
                    var frame = _captureEngine.CaptureFullFrame(out var width, out var height);
                    if (frame.Length == 0)
                    {
                        continue;
                    }

                    capturedAny = true;
                    foreach (var region in group)
                    {
                        var cropped = CaptureEngine.CropBgra(frame, width, height, region.Source);
                        var destW = region.Destination.Width;
                        var destH = region.Destination.Height;
                        var srcW = region.Source.Width;
                        var srcH = region.Source.Height;
                        var payload = srcW != destW || srcH != destH
                            ? CaptureEngine.ScaleBgra(cropped, srcW, srcH, destW, destH)
                            : cropped;

                        _sharedFrameBridge.WriteFrame(region.Index, payload, destW, destH);
                    }
                }

                if (!capturedAny)
                {
                    await Task.Delay(50, stoppingToken);
                    continue;
                }

                Interlocked.Increment(ref _framesCaptured);
                // ~60 FPS hedefi; çözünürlük değişmez, sadece kare aralığı kısalır.
                await Task.Delay(16, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Capture döngüsünde hata.");
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}

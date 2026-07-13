using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using VDisplay.Core;
using VDisplay.Core.Models;

namespace VDisplay.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NamedPipeIpcClient _client = new();

    [ObservableProperty]
    private string _statusText = "Durum bilinmiyor";

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private int _selectedMonitorCountIndex = 0;

    [ObservableProperty]
    private int _selectedLayoutIndex = 0;

    [ObservableProperty]
    private int _selectedSourceMonitorIndex = 0;

    [ObservableProperty]
    private string _messageColor = "Gray";

    public ObservableCollection<MonitorItemViewModel> Monitors { get; } = [];
    public ObservableCollection<PhysicalMonitorItemViewModel> PhysicalMonitors { get; } = [];
    public ObservableCollection<string> LayoutOptions { get; } =
    [
        "2 Dikey", "2 Yatay", "3 Dikey", "4 Grid"
    ];
    public ObservableCollection<string> MonitorCountOptions { get; } =
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];

    public MainViewModel()
    {
    }

    [RelayCommand]
    private async Task StartDriverAsync() => await ExecuteAsync(IpcCommand.StartDriver);

    [RelayCommand]
    private async Task StopDriverAsync() => await ExecuteAsync(IpcCommand.StopDriver);

    [RelayCommand]
    private async Task RefreshStatusAsync() => await ExecuteAsync(IpcCommand.GetStatus);

    [RelayCommand]
    private async Task ApplyMonitorCountAsync()
    {
        var count = SelectedMonitorCountIndex + 1;
        var payload = JsonSerializer.Serialize(new { count });
        await ExecuteAsync(IpcCommand.SetMonitorCount, payload);
    }

    [RelayCommand]
    private async Task ApplyLayoutAsync()
    {
        var layout = SelectedLayoutIndex switch
        {
            0 => LayoutType.TwoVertical,
            1 => LayoutType.TwoHorizontal,
            2 => LayoutType.ThreeVertical,
            3 => LayoutType.FourGrid,
            _ => LayoutType.TwoVertical
        };

        var payload = JsonSerializer.Serialize(new { layout = layout.ToString() });
        await ExecuteAsync(IpcCommand.SetLayout, payload);
    }

    [RelayCommand]
    private async Task ApplySourceMonitorAsync()
    {
        var monitorIndex = SelectedSourceMonitorIndex;
        if (PhysicalMonitors.Count > 0 && SelectedSourceMonitorIndex >= 0 && SelectedSourceMonitorIndex < PhysicalMonitors.Count)
        {
            monitorIndex = PhysicalMonitors[SelectedSourceMonitorIndex].Index;
        }

        var payload = JsonSerializer.Serialize(new { index = monitorIndex });
        await ExecuteAsync(IpcCommand.SetSourceMonitor, payload);
    }

    [RelayCommand]
    private async Task StartCaptureAsync() => await ExecuteAsync(IpcCommand.StartCapture);

    [RelayCommand]
    private async Task StopCaptureAsync() => await ExecuteAsync(IpcCommand.StopCapture);

    [RelayCommand]
    private async Task LoadPhysicalMonitorsAsync()
    {
        try
        {
            var response = await _client.SendAsync(new IpcRequest { Command = IpcCommand.GetPhysicalMonitors });
            if (!response.Success || string.IsNullOrWhiteSpace(response.Data))
            {
                return;
            }

            var monitors = JsonSerializer.Deserialize<List<PhysicalMonitorInfo>>(response.Data);
            PhysicalMonitors.Clear();
            if (monitors is null)
            {
                return;
            }

            foreach (var monitor in monitors)
            {
                PhysicalMonitors.Add(new PhysicalMonitorItemViewModel
                {
                    Index = monitor.Index,
                    Label = $"{monitor.Name} ({monitor.Width}x{monitor.Height}){(monitor.IsPrimary ? " *" : "")}"
                });
            }

            if (PhysicalMonitors.Count > 0 && SelectedSourceMonitorIndex >= PhysicalMonitors.Count)
            {
                SelectedSourceMonitorIndex = 0;
            }
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, false);
        }
    }

    private async Task ExecuteAsync(IpcCommand command, string? payload = null)
    {
        try
        {
            var response = await _client.SendAsync(new IpcRequest
            {
                Command = command,
                Payload = payload
            });

            if (!response.Success)
            {
                SetMessage(response.Message ?? "İşlem başarısız.", false);
                return;
            }

            if (command is IpcCommand.GetStatus or IpcCommand.SetMonitorCount or IpcCommand.SetLayout
                or IpcCommand.SetSourceMonitor or IpcCommand.StartCapture or IpcCommand.StopCapture
                && !string.IsNullOrWhiteSpace(response.Data))
            {
                ApplyStatus(response.Data);
            }

            if (!string.IsNullOrWhiteSpace(response.Message))
            {
                SetMessage(response.Message, true);
            }
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, false);
        }
    }

    private void ApplyStatus(string json)
    {
        var status = JsonSerializer.Deserialize<ServiceStatus>(json);
        if (status is null)
        {
            return;
        }

        StatusText = status.DriverRunning
            ? $"Sürücü aktif • {status.MonitorCount} VM • Düzen: {status.CurrentLayout}" +
              (status.CaptureRunning ? $" • Capture: {status.FramesCaptured} kare" : "")
            : "Sürücü kapalı";

        Monitors.Clear();
        foreach (var monitor in status.Monitors)
        {
            Monitors.Add(new MonitorItemViewModel
            {
                Name = $"{monitor.Name} {(monitor.IsActive ? "(Aktif)" : "(Pasif)")}",
                Resolution = $"{monitor.Width}x{monitor.Height} @ {monitor.RefreshRate}Hz"
            });
        }

        OnPropertyChanged(nameof(Monitors));

        if (status.MonitorCount is >= 1 and <= 10)
        {
            SelectedMonitorCountIndex = status.MonitorCount - 1;
        }

        SelectedLayoutIndex = status.CurrentLayout switch
        {
            LayoutType.TwoVertical => 0,
            LayoutType.TwoHorizontal => 1,
            LayoutType.ThreeVertical => 2,
            LayoutType.FourGrid => 3,
            _ => 0
        };

        SelectedSourceMonitorIndex = 0;
        for (var i = 0; i < PhysicalMonitors.Count; i++)
        {
            if (PhysicalMonitors[i].Index == status.SourceMonitorIndex)
            {
                SelectedSourceMonitorIndex = i;
                break;
            }
        }
    }

    private void SetMessage(string text, bool success)
    {
        Message = text;
        MessageColor = success ? "ForestGreen" : "IndianRed";
    }
}

public sealed class MonitorItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
}

public sealed class PhysicalMonitorItemViewModel
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
}

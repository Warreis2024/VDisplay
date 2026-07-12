using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using VDisplay.Core;
using VDisplay.Core.Models;

namespace VDisplay.App;

public sealed partial class MainWindow : Window
{
    private readonly NamedPipeIpcClient _client = new();
    private TextBlock _statusText = null!;
    private TextBlock _monitorListText = null!;
    private TextBlock _messageText = null!;

    public MainWindow()
    {
        InitializeComponent();
        Title = "VDisplay";
        BuildUi();
        Activated += OnFirstActivated;
    }

    private void BuildUi()
    {
        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock { Text = "Sanal monitor yoneticisi", Opacity = 0.7 });

        var topButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        topButtons.Children.Add(MakeButton("Surucu Baslat", OnStartDriverClick));
        topButtons.Children.Add(MakeButton("Surucu Durdur", OnStopDriverClick));
        topButtons.Children.Add(MakeButton("Capture Baslat", OnStartCaptureClick));
        topButtons.Children.Add(MakeButton("Capture Durdur", OnStopCaptureClick));
        topButtons.Children.Add(MakeButton("Yenile", OnRefreshClick));
        panel.Children.Add(topButtons);

        _statusText = new TextBlock { FontSize = 16 };
        panel.Children.Add(_statusText);

        panel.Children.Add(new TextBlock { Text = "Duzen:" });
        var layoutButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        layoutButtons.Children.Add(MakeButton("2 Dikey", OnLayoutClick, "TwoVertical"));
        layoutButtons.Children.Add(MakeButton("2 Yatay", OnLayoutClick, "TwoHorizontal"));
        layoutButtons.Children.Add(MakeButton("3 Dikey", OnLayoutClick, "ThreeVertical"));
        layoutButtons.Children.Add(MakeButton("4 Grid", OnLayoutClick, "FourGrid"));
        panel.Children.Add(layoutButtons);

        panel.Children.Add(new TextBlock { Text = "VM sayisi:" });
        var vmButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        vmButtons.Children.Add(MakeButton("2", OnVmCountClick, "2"));
        vmButtons.Children.Add(MakeButton("3", OnVmCountClick, "3"));
        vmButtons.Children.Add(MakeButton("4", OnVmCountClick, "4"));
        panel.Children.Add(vmButtons);

        _monitorListText = new TextBlock();
        panel.Children.Add(_monitorListText);

        _messageText = new TextBlock();
        panel.Children.Add(_messageText);

        Root.Children.Add(panel);
    }

    private static Button MakeButton(string content, RoutedEventHandler click, string? tag = null)
    {
        var button = new Button { Content = content };
        if (tag is not null)
        {
            button.Tag = tag;
        }

        button.Click += click;
        return button;
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        await RefreshAsync();
    }

    private async void OnStartDriverClick(object sender, RoutedEventArgs e) =>
        await SendAsync(IpcCommand.StartDriver);

    private async void OnStopDriverClick(object sender, RoutedEventArgs e) =>
        await SendAsync(IpcCommand.StopDriver);

    private async void OnStartCaptureClick(object sender, RoutedEventArgs e) =>
        await SendAsync(IpcCommand.StartCapture);

    private async void OnStopCaptureClick(object sender, RoutedEventArgs e) =>
        await SendAsync(IpcCommand.StopCapture);

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnLayoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string layout })
        {
            var payload = JsonSerializer.Serialize(new { layout });
            await SendAsync(IpcCommand.SetLayout, payload);
        }
    }

    private async void OnVmCountClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string countStr } && int.TryParse(countStr, out var count))
        {
            var payload = JsonSerializer.Serialize(new { count });
            await SendAsync(IpcCommand.SetMonitorCount, payload);
        }
    }

    private async Task RefreshAsync() => await SendAsync(IpcCommand.GetStatus);

    private async Task SendAsync(IpcCommand command, string? payload = null)
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
                _messageText.Text = response.Message ?? "Hata";
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.Data)
                && command is IpcCommand.GetStatus or IpcCommand.SetLayout
                    or IpcCommand.SetMonitorCount or IpcCommand.StartCapture
                    or IpcCommand.StopCapture or IpcCommand.StartDriver)
            {
                ApplyStatus(response.Data);
            }

            if (!string.IsNullOrWhiteSpace(response.Message))
            {
                _messageText.Text = response.Message;
            }
        }
        catch (Exception ex)
        {
            _messageText.Text = ex.Message;
        }
    }

    private void ApplyStatus(string json)
    {
        var status = JsonSerializer.Deserialize<ServiceStatus>(json);
        if (status is null)
        {
            return;
        }

        _statusText.Text = status.DriverRunning
            ? $"Surucu: ACIK | VM: {status.MonitorCount} | Duzen: {status.CurrentLayout}" +
              (status.CaptureRunning ? $" | Capture: {status.FramesCaptured} kare" : "")
            : "Surucu: KAPALI";

        _monitorListText.Text = status.Monitors.Count == 0
            ? "Sanal monitor yok"
            : string.Join("\n", status.Monitors.Select(m =>
                $"{m.Name} {m.Width}x{m.Height} {(m.IsActive ? "(aktif)" : "")}"));
    }
}

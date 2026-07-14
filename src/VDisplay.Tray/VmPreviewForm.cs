using System.Drawing;
using System.Runtime.Versioning;
using VDisplay.Core.Models;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class VmPreviewForm : Form
{
    // Büyük önizleme: ~60 FPS
    private const int TargetFrameMs = 16;

    private readonly PhysicalMonitorInfo _monitor;
    private readonly VmFrameSource _source;
    private readonly PictureBox _picture;
    private readonly Label _hint;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _refreshRunning;
    private int _uiBusy;
    private bool _controlEnabled = true;
    private bool _injecting;
    private bool _pictureOwnsImage;
    private MouseButtons _pressedButtons;

    public VmPreviewForm(PhysicalMonitorInfo monitor, int sharedIndex, string title)
    {
        _monitor = monitor;
        _source = new VmFrameSource(monitor, sharedIndex, useSharedMemory: true);
        Text = $"{title} — uzaktan kontrol";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(960, 600);
        MinimumSize = new Size(480, 320);
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        _hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.FromArgb(140, 220, 140),
            Text = HintText(controlOn: true)
        };

        _picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Cross,
            TabStop = true
        };
        Controls.Add(_picture);
        Controls.Add(_hint);

        WireInput();

        _picture.MouseEnter += (_, _) => _picture.Focus();
        MouseWheel += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            var client = _picture.PointToClient(Cursor.Position);
            InjectMove(client);
            InputInjector.MouseWheelDelta(e.Delta);
        };

        Shown += (_, _) =>
        {
            PreviewSession.SetActive(this);
            _picture.Focus();
            StartRefreshLoop();
        };
        FormClosed += (_, _) =>
        {
            PreviewSession.SetActive(null);
            _cts.Cancel();
            if (_pictureOwnsImage)
            {
                _picture.Image?.Dispose();
            }

            _picture.Image = null;
            _source.Dispose();
        };
    }

    public void ToggleControlPublic() => ToggleControl();

    public void ReturnToPrimaryPublic() => ReturnToPrimary();

    private static string HintText(bool controlOn) => controlOn
        ? "Kontrol AÇIK — tıkla: VM'ye git | F3: primary'ye dön | F2: aç/kapa | Esc: kapat"
        : "Kontrol KAPALI — sadece izleme | F3: primary | F2: aç | Esc: kapat";

    private void WireInput()
    {
        _picture.MouseMove += (_, e) =>
        {
            if (!_controlEnabled || _injecting)
            {
                return;
            }

            InjectMove(e.Location);
        };

        _picture.MouseDown += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            _picture.Capture = true;
            _pressedButtons |= e.Button;
            InjectButton(e.Location, e.Button, down: true);
        };

        _picture.MouseUp += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            _pressedButtons &= ~e.Button;
            InjectButton(e.Location, e.Button, down: false);
            if (_pressedButtons == MouseButtons.None)
            {
                _picture.Capture = false;
            }
        };

        _picture.MouseWheel += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            InjectMove(e.Location);
            InputInjector.MouseWheelDelta(e.Delta);
        };

        KeyDown += OnPreviewKeyDown;
        KeyUp += OnPreviewKeyUp;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Odağı kaybetmeden önce güvenilir yakalama
        if (keyData == Keys.F3)
        {
            ReturnToPrimary();
            return true;
        }

        if (keyData == Keys.F2)
        {
            ToggleControl();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Escape or Keys.F2 or Keys.F3)
        {
            // ProcessCmdKey / global hotkey zaten işler — VM'ye enjekte etme
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (!_controlEnabled)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        InputInjector.Key(e.KeyCode, down: true);
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_controlEnabled || e.KeyCode is Keys.Escape or Keys.F2 or Keys.F3)
        {
            return;
        }

        e.Handled = true;
        InputInjector.Key(e.KeyCode, down: false);
    }

    private void ToggleControl()
    {
        _controlEnabled = !_controlEnabled;
        _picture.Cursor = _controlEnabled ? Cursors.Cross : Cursors.Default;
        _hint.Text = HintText(_controlEnabled);
        _hint.ForeColor = _controlEnabled
            ? Color.FromArgb(140, 220, 140)
            : Color.FromArgb(200, 200, 200);
    }

    private void ReturnToPrimary()
    {
        _pressedButtons = MouseButtons.None;
        _picture.Capture = false;
        _injecting = false;

        PreviewSession.MoveCursorToPrimary();

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        // Oyuna/başka pencereye giden odağı geri al + imleç primary'de kalsın
        PreviewSession.ForceForeground(Handle);
        BringToFront();
        Activate();
        _picture.Focus();

        // Bir kez daha — odak/yarış sonrası imleç VM'ye geri kaçmasın
        BeginInvoke(PreviewSession.MoveCursorToPrimary);
    }

    private void InjectMove(Point clientPt)
    {
        if (!TryMapToVm(clientPt, out var screenX, out var screenY))
        {
            return;
        }

        _injecting = true;
        try
        {
            InputInjector.MoveTo(screenX, screenY);
        }
        finally
        {
            _injecting = false;
        }
    }

    private void InjectButton(Point clientPt, MouseButtons button, bool down)
    {
        if (!TryMapToVm(clientPt, out var screenX, out var screenY))
        {
            return;
        }

        _injecting = true;
        try
        {
            InputInjector.MoveTo(screenX, screenY);
            InputInjector.MouseButton(button, down);
        }
        finally
        {
            _injecting = false;
        }
    }

    private bool TryMapToVm(Point clientPt, out int screenX, out int screenY)
    {
        screenX = 0;
        screenY = 0;

        var image = _picture.Image;
        if (image is null || _monitor.Width <= 0 || _monitor.Height <= 0)
        {
            return false;
        }

        var box = _picture.ClientSize;
        if (box.Width <= 0 || box.Height <= 0)
        {
            return false;
        }

        var scale = Math.Min((float)box.Width / image.Width, (float)box.Height / image.Height);
        var dispW = Math.Max(1, (int)(image.Width * scale));
        var dispH = Math.Max(1, (int)(image.Height * scale));
        var offX = (box.Width - dispW) / 2;
        var offY = (box.Height - dispH) / 2;

        if (clientPt.X < offX || clientPt.Y < offY
            || clientPt.X >= offX + dispW || clientPt.Y >= offY + dispH)
        {
            return false;
        }

        var u = (clientPt.X - offX) / (float)dispW;
        var v = (clientPt.Y - offY) / (float)dispH;
        u = Math.Clamp(u, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);

        screenX = _monitor.X + (int)(u * (_monitor.Width - 1));
        screenY = _monitor.Y + (int)(v * (_monitor.Height - 1));
        return true;
    }

    private void StartRefreshLoop()
    {
        if (_refreshRunning)
        {
            return;
        }

        _refreshRunning = true;
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested && !IsDisposed)
            {
                var started = Environment.TickCount64;
                try
                {
                    if (Interlocked.CompareExchange(ref _uiBusy, 1, 0) != 0)
                    {
                        await Task.Delay(4, _cts.Token);
                        continue;
                    }

                    if (!_source.TryCaptureFrame(preview: true, out var frame, out var owns)
                        || frame is null
                        || IsDisposed
                        || _cts.IsCancellationRequested)
                    {
                        if (owns)
                        {
                            frame?.Dispose();
                        }

                        Interlocked.Exchange(ref _uiBusy, 0);
                    }
                    else
                    {
                        BeginInvoke(() =>
                        {
                            try
                            {
                                if (IsDisposed)
                                {
                                    if (owns)
                                    {
                                        frame.Dispose();
                                    }

                                    return;
                                }

                                if (ReferenceEquals(_picture.Image, frame))
                                {
                                    _picture.Invalidate();
                                    return;
                                }

                                if (_pictureOwnsImage)
                                {
                                    _picture.Image?.Dispose();
                                }

                                _picture.Image = frame;
                                _pictureOwnsImage = owns;
                            }
                            finally
                            {
                                Interlocked.Exchange(ref _uiBusy, 0);
                            }
                        });
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var elapsed = Environment.TickCount64 - started;
                var delay = Math.Max(0, TargetFrameMs - (int)elapsed);
                if (delay > 0)
                {
                    try
                    {
                        await Task.Delay(delay, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _refreshRunning = false;
        });
    }
}

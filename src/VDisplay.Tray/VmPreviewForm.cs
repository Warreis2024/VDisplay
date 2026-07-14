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
    private readonly int _sharedIndex;
    private readonly VmFrameSource _source;
    private readonly SharedGpuFrameReader _gpuReader;
    private readonly PictureBox _picture;
    private readonly Label _hint;
    private readonly CancellationTokenSource _cts = new();
    private D3DPreviewPanel? _d3d;
    private Control _surface;
    private volatile bool _refreshRunning;
    private volatile bool _useGpu;
    private int _gpuFailCount;
    private int _uiBusy;
    private bool _controlEnabled = true;
    private bool _injecting;
    private bool _pictureOwnsImage;
    private MouseButtons _pressedButtons;

    public VmPreviewForm(PhysicalMonitorInfo monitor, int sharedIndex, string title)
    {
        _monitor = monitor;
        _sharedIndex = sharedIndex;
        _source = new VmFrameSource(monitor, sharedIndex, useSharedMemory: true);
        _gpuReader = new SharedGpuFrameReader();
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
        _surface = _picture;
        Controls.Add(_picture);
        Controls.Add(_hint);

        WireInput(_picture);
        KeyDown += OnPreviewKeyDown;
        KeyUp += OnPreviewKeyUp;

        _picture.MouseEnter += (_, _) => _picture.Focus();
        MouseWheel += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            var client = _surface.PointToClient(Cursor.Position);
            InjectMove(client);
            InputInjector.MouseWheelDelta(e.Delta);
        };

        Shown += (_, _) =>
        {
            PreviewSession.SetActive(this);
            TryEnableGpuPath();
            _surface.Focus();
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
            _d3d?.Dispose();
            _gpuReader.Dispose();
            _source.Dispose();
        };
    }

    public void ToggleControlPublic() => ToggleControl();

    public void ReturnToPrimaryPublic() => ReturnToPrimary();

    private void TryEnableGpuPath()
    {
        try
        {
            var panel = new D3DPreviewPanel(_sharedIndex, _gpuReader)
            {
                Dock = DockStyle.Fill,
                Cursor = Cursors.Cross,
                TabStop = true
            };
            if (!panel.TryStart())
            {
                panel.Dispose();
                return;
            }

            Controls.Add(panel);
            Controls.SetChildIndex(panel, 0);
            _picture.Visible = false;
            _d3d = panel;
            _surface = panel;
            _useGpu = true;
            WireInput(panel);
            panel.MouseEnter += (_, _) => panel.Focus();
        }
        catch
        {
            _useGpu = false;
        }
    }

    private static string HintText(bool controlOn) => controlOn
        ? "Kontrol AÇIK — tıkla: VM'ye git | F3: primary'ye dön | F2: aç/kapa | Esc: kapat"
        : "Kontrol KAPALI — sadece izleme | F3: primary | F2: aç | Esc: kapat";

    private void WireInput(Control surface)
    {
        surface.MouseMove += (_, e) =>
        {
            if (!_controlEnabled || _injecting)
            {
                return;
            }

            InjectMove(e.Location);
        };

        surface.MouseDown += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            surface.Capture = true;
            _pressedButtons |= e.Button;
            InjectButton(e.Location, e.Button, down: true);
        };

        surface.MouseUp += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            _pressedButtons &= ~e.Button;
            InjectButton(e.Location, e.Button, down: false);
            if (_pressedButtons == MouseButtons.None)
            {
                surface.Capture = false;
            }
        };

        surface.MouseWheel += (_, e) =>
        {
            if (!_controlEnabled)
            {
                return;
            }

            InjectMove(e.Location);
            InputInjector.MouseWheelDelta(e.Delta);
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
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
        _surface.Cursor = _controlEnabled ? Cursors.Cross : Cursors.Default;
        _hint.Text = HintText(_controlEnabled);
        _hint.ForeColor = _controlEnabled
            ? Color.FromArgb(140, 220, 140)
            : Color.FromArgb(200, 200, 200);
    }

    private void ReturnToPrimary()
    {
        _pressedButtons = MouseButtons.None;
        _surface.Capture = false;
        _injecting = false;

        PreviewSession.MoveCursorToPrimary();

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        PreviewSession.ForceForeground(Handle);
        BringToFront();
        Activate();
        _surface.Focus();

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
        if (_monitor.Width <= 0 || _monitor.Height <= 0)
        {
            return false;
        }

        int offX;
        int offY;
        int dispW;
        int dispH;

        if (_useGpu && _d3d is not null)
        {
            var bounds = _d3d.ContentBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            offX = bounds.X;
            offY = bounds.Y;
            dispW = bounds.Width;
            dispH = bounds.Height;
        }
        else
        {
            var image = _picture.Image;
            if (image is null)
            {
                return false;
            }

            var box = _picture.ClientSize;
            if (box.Width <= 0 || box.Height <= 0)
            {
                return false;
            }

            var scale = Math.Min((float)box.Width / image.Width, (float)box.Height / image.Height);
            dispW = Math.Max(1, (int)(image.Width * scale));
            dispH = Math.Max(1, (int)(image.Height * scale));
            offX = (box.Width - dispW) / 2;
            offY = (box.Height - dispH) / 2;
        }

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

                    if (_useGpu && _d3d is not null)
                    {
                        BeginInvoke(() =>
                        {
                            try
                            {
                                if (IsDisposed)
                                {
                                    return;
                                }

                                if (!_d3d.TryPresent())
                                {
                                    _gpuFailCount++;
                                    if (_gpuFailCount > 120)
                                    {
                                        FallbackToBgra();
                                    }
                                }
                                else
                                {
                                    _gpuFailCount = 0;
                                }
                            }
                            finally
                            {
                                Interlocked.Exchange(ref _uiBusy, 0);
                            }
                        });
                    }
                    else if (!_source.TryCaptureFrame(preview: true, out var frame, out var owns)
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

    private void FallbackToBgra()
    {
        _useGpu = false;
        if (_d3d is not null)
        {
            Controls.Remove(_d3d);
            _d3d.Dispose();
            _d3d = null;
        }

        _picture.Visible = true;
        _surface = _picture;
        _picture.BringToFront();
    }
}

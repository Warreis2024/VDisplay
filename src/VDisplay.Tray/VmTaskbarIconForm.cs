using System.Drawing;
using System.Runtime.Versioning;
using VDisplay.Core.Models;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class VmTaskbarIconForm : Form
{
    private const int IconWidth = 88;
    private const int IconHeight = 66;

    private readonly PhysicalMonitorInfo _monitor;
    private readonly int _sharedIndex;
    private readonly VmFrameSource _source;
    private readonly PictureBox _thumb;
    private readonly System.Windows.Forms.Timer _timer;
    private Bitmap? _miniBitmap;
    private VmPreviewForm? _preview;

    public string MonitorKey => $"{_monitor.Name}|{_monitor.X}|{_monitor.Y}|{_monitor.Width}|{_monitor.Height}";

    public VmTaskbarIconForm(PhysicalMonitorInfo monitor, int vmNumber, int sharedIndex)
    {
        _monitor = monitor;
        _sharedIndex = sharedIndex;
        _source = new VmFrameSource(monitor, sharedIndex, useSharedMemory: true);

        Text = monitor.VmLabel(vmNumber);
        // Keep thumbnails off the Windows taskbar (only tray + floating previews).
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(IconWidth, IconHeight);
        BackColor = Color.FromArgb(32, 32, 32);

        _thumb = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand
        };
        Controls.Add(_thumb);

        _thumb.Click += (_, _) => OpenPreview();
        Click += (_, _) => OpenPreview();

        // Mini pencereler yavaş (~0.5 FPS) — GPU yükünü düşür; büyütülünce preview hızlı
        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) => RefreshThumbnail();
        Shown += (_, _) =>
        {
            // Index ile hafif kaydırma: 10 mini aynı anda DXGI/GDI vurmasın
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(50 * Math.Max(0, sharedIndex));
                    if (!IsDisposed)
                    {
                        BeginInvoke(() =>
                        {
                            if (!IsDisposed)
                            {
                                _timer.Start();
                                RefreshThumbnail();
                            }
                        });
                    }
                }
                catch
                {
                    // ignore
                }
            });
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _thumb.Image = null;
            _miniBitmap?.Dispose();
            _miniBitmap = null;
            _source.Dispose();
            _preview?.Close();
        };
    }

    public void SetDockPosition(int index, int total, Rectangle workArea)
    {
        var gap = 6;
        var totalWidth = (total * Width) + ((total - 1) * gap);
        var startX = workArea.Left + Math.Max(0, (workArea.Width - totalWidth) / 2);
        Location = new Point(startX + index * (Width + gap), workArea.Bottom - Height - 8);
    }

    private void OpenPreview()
    {
        if (_preview is { IsDisposed: false })
        {
            _preview.BringToFront();
            _preview.WindowState = FormWindowState.Normal;
            _preview.Focus();
            return;
        }

        // Bu VM büyütülüyken mini yenilemeyi durdur
        _timer.Stop();
        _preview = new VmPreviewForm(_monitor, _sharedIndex, Text);
        _preview.FormClosed += (_, _) =>
        {
            _preview = null;
            if (!IsDisposed)
            {
                _timer.Start();
                RefreshThumbnail();
            }
        };
        _preview.Show();
    }

    private void RefreshThumbnail()
    {
        if (!_source.TryCaptureThumbnail(out var frame, out var owns) || frame is null)
        {
            return;
        }

        try
        {
            if (_miniBitmap is null)
            {
                _miniBitmap = new Bitmap(IconWidth, IconHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }

            using (var g = Graphics.FromImage(_miniBitmap))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(frame, 0, 0, IconWidth, IconHeight);
            }

            if (ReferenceEquals(_thumb.Image, _miniBitmap))
            {
                _thumb.Invalidate();
            }
            else
            {
                _thumb.Image = _miniBitmap;
            }
        }
        finally
        {
            if (owns)
            {
                frame.Dispose();
            }
        }
    }
}

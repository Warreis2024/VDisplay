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
    private VmPreviewForm? _preview;

    public string MonitorKey => $"{_monitor.Name}|{_monitor.X}|{_monitor.Y}|{_monitor.Width}|{_monitor.Height}";

    public VmTaskbarIconForm(PhysicalMonitorInfo monitor, int vmNumber, int sharedIndex)
    {
        _monitor = monitor;
        _sharedIndex = sharedIndex;
        _source = new VmFrameSource(monitor, sharedIndex, useSharedMemory: true);

        Text = $"VM {vmNumber}";
        ShowInTaskbar = true;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = true;
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

        _timer = new System.Windows.Forms.Timer { Interval = 400 };
        _timer.Tick += (_, _) => RefreshThumbnail();
        Shown += (_, _) =>
        {
            _timer.Start();
            RefreshThumbnail();
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _source.Dispose();
            _thumb.Image?.Dispose();
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

        _preview = new VmPreviewForm(_monitor, _sharedIndex, Text);
        _preview.FormClosed += (_, _) => _preview = null;
        _preview.Show();
    }

    private void RefreshThumbnail()
    {
        var frame = _source.CaptureThumbnail();
        if (frame is null)
        {
            return;
        }

        var old = _thumb.Image;
        _thumb.Image = new Bitmap(frame, new Size(IconWidth, IconHeight));
        old?.Dispose();
        frame.Dispose();
    }
}

using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using VDisplay.Core.Models;

namespace VDisplay.Overlay;

[SupportedOSPlatform("windows")]
internal sealed class SplitOverlayForm : Form
{
    private readonly PhysicalMonitorInfo _physical;
    private readonly PictureBox _leftBox;
    private readonly PictureBox _rightBox;

    public SplitOverlayForm(PhysicalMonitorInfo physical)
    {
        _physical = physical;
        Text = "VDisplay Split";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(physical.X, physical.Y, physical.Width, physical.Height);
        TopMost = true;
        BackColor = Color.Black;
        KeyPreview = true;
        KeyDown += OnKeyDown;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 2f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        _leftBox = CreateBox();
        _rightBox = CreateBox();
        grid.Controls.Add(Wrap(_leftBox), 0, 0);
        grid.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(200, 200, 200) }, 1, 0);
        grid.Controls.Add(Wrap(_rightBox), 2, 0);
        Controls.Add(grid);
    }

    public void CaptureAndApply()
    {
        Visible = false;
        Application.DoEvents();
        Thread.Sleep(30);

        try
        {
            SetBitmap(_leftBox, ScreenCapture.CaptureLeftHalf(_physical));
            SetBitmap(_rightBox, ScreenCapture.CaptureRightHalf(_physical));
        }
        finally
        {
            Visible = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            return;
        }

        if (e.KeyCode == Keys.F5)
        {
            CaptureAndApply();
        }
    }

    private static PictureBox CreateBox() => new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.StretchImage,
        BackColor = Color.Black
    };

    private static Panel Wrap(Control box)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        p.Controls.Add(box);
        return p;
    }

    private static void SetBitmap(PictureBox box, Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        var old = box.Image;
        box.Image = bitmap;
        old?.Dispose();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _leftBox.Image?.Dispose();
        _rightBox.Image?.Dispose();
        base.OnFormClosed(e);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class HybridOverlayForm : Form
{
    private readonly PhysicalMonitorInfo _physical;
    private readonly PictureBox _box;
    private readonly int _x;
    private readonly int _y;
    private readonly int _w;
    private readonly int _h;

    public HybridOverlayForm(PhysicalMonitorInfo physical)
    {
        _physical = physical;
        var halfW = Math.Max(1, physical.Width / 2);
        _x = physical.X + halfW;
        _y = physical.Y;
        _w = physical.Width - halfW;
        _h = physical.Height;

        Text = "VDisplay Hybrid";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(_x, _y, _w, _h);
        TopMost = true;
        BackColor = Color.Black;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.F5)
            {
                CaptureAndApply();
            }
        };

        _box = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Black };
        Controls.Add(_box);
    }

    public void CaptureAndApply()
    {
        Visible = false;
        Application.DoEvents();
        Thread.Sleep(30);
        try
        {
            var bmp = ScreenCapture.CaptureRegion(_x, _y, _w, _h);
            if (bmp is not null)
            {
                var old = _box.Image;
                _box.Image = bmp;
                old?.Dispose();
            }
        }
        finally
        {
            Visible = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _box.Image?.Dispose();
        base.OnFormClosed(e);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class DividerForm : Form
{
    public DividerForm(PhysicalMonitorInfo physical)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        var cx = physical.X + Math.Max(0, physical.Width / 2 - 1);
        Bounds = new Rectangle(cx, physical.Y, 2, physical.Height);
        TopMost = true;
        BackColor = Color.FromArgb(200, 200, 200);
    }
}

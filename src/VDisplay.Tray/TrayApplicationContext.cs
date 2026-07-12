using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly List<VmTaskbarIconForm> _icons = [];
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;

    public TrayApplicationContext()
    {
        _trayIcon = CreateEmojiIcon("🖥️");
        _notifyIcon = new NotifyIcon
        {
            Text = "VDisplay VM",
            Visible = true,
            Icon = _trayIcon
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("VM ikonlarini yenile", null, (_, _) => SyncIcons());
        menu.Items.Add("Cikis", null, (_, _) => ExitThread());
        _notifyIcon.ContextMenuStrip = menu;

        _pollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _pollTimer.Tick += (_, _) => SyncIcons();
        _pollTimer.Start();

        SyncIcons();
    }

    private void SyncIcons()
    {
        var vms = VirtualMonitorDiscovery.GetVirtualMonitors();
        if (vms.Count == 0)
        {
            _notifyIcon.Text = "VDisplay VM (VM yok)";
            ClearIcons();
            return;
        }

        _notifyIcon.Text = $"VDisplay VM ({vms.Count})";

        if (_icons.Count == vms.Count
            && _icons.Zip(vms).All(pair => pair.First.MonitorKey == MonitorKey(pair.Second)))
        {
            LayoutIcons(vms.Count);
            return;
        }

        ClearIcons();

        for (var i = 0; i < vms.Count; i++)
        {
            var vm = vms[i];
            var form = new VmTaskbarIconForm(vm, i + 1, i);
            form.Show();
            _icons.Add(form);
        }

        LayoutIcons(vms.Count);
    }

    private static string MonitorKey(Core.Models.PhysicalMonitorInfo monitor) =>
        $"{monitor.Name}|{monitor.X}|{monitor.Y}|{monitor.Width}|{monitor.Height}";

    private void LayoutIcons(int count)
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var work = screen.WorkingArea;

        for (var i = 0; i < _icons.Count; i++)
        {
            _icons[i].SetDockPosition(i, count, work);
        }
    }

    private void ClearIcons()
    {
        foreach (var icon in _icons)
        {
            icon.Close();
            icon.Dispose();
        }

        _icons.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
            ClearIcons();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Icon CreateEmojiIcon(string emoji)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var font = new Font("Segoe UI Emoji", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
            var bounds = new RectangleF(0, 0, size, size);
            TextRenderer.DrawText(
                g,
                emoji,
                font,
                Rectangle.Round(bounds),
                Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

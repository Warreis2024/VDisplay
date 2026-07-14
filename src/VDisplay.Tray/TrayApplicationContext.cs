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
    private readonly GlobalHotkeyWindow _hotkeys;

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

        _hotkeys = new GlobalHotkeyWindow();
        _hotkeys.Bind(
            onF2: PreviewSession.ToggleControlOnActive,
            onF3: PreviewSession.ReturnActiveToPrimary);

        _pollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _pollTimer.Tick += (_, _) => SyncIcons();
        _pollTimer.Start();

        SyncIcons();
    }

    private bool _warnedNoVm;
    private bool _syncing;

    private void SyncIcons()
    {
        // Close() mesaj pompalayabilir → Timer tekrar SyncIcons çağırır (reentrancy).
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var vms = VirtualMonitorDiscovery.GetVirtualMonitors();
            if (vms.Count == 0)
            {
                _notifyIcon.Text = "VDisplay VM (VM yok — sürücü/start?)";
                if (!_warnedNoVm)
                {
                    _warnedNoVm = true;
                    _notifyIcon.BalloonTipTitle = "VDisplay";
                    _notifyIcon.BalloonTipText = "Sanal monitör yok. Helper'da İlk kurulum + Başlat gerekir.";
                    _notifyIcon.ShowBalloonTip(4000);
                }

                ClearIcons();
                return;
            }

            _warnedNoVm = false;
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
        finally
        {
            _syncing = false;
        }
    }

    private static string MonitorKey(Core.Models.PhysicalMonitorInfo monitor) =>
        $"{monitor.Name}|{monitor.X}|{monitor.Y}|{monitor.Width}|{monitor.Height}";

    private void LayoutIcons(int count)
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var work = screen.WorkingArea;
        var icons = _icons.ToArray();

        for (var i = 0; i < icons.Length; i++)
        {
            if (!icons[i].IsDisposed)
            {
                icons[i].SetDockPosition(i, count, work);
            }
        }
    }

    private void ClearIcons()
    {
        // Önce kopyala+temizle: Close() sırasında enumerator kırılmasın.
        var icons = _icons.ToArray();
        _icons.Clear();

        foreach (var icon in icons)
        {
            try
            {
                if (!icon.IsDisposed)
                {
                    icon.Close();
                    icon.Dispose();
                }
            }
            catch
            {
                // ignore dispose races during display mode change
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
            ClearIcons();
            _hotkeys.Dispose();
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

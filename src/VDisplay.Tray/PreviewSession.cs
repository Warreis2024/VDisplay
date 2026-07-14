using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal static class PreviewSession
{
    private static readonly object Sync = new();
    private static VmPreviewForm? _active;

    public static void SetActive(VmPreviewForm? form)
    {
        lock (Sync)
        {
            _active = form;
        }
    }

    public static void ToggleControlOnActive()
    {
        VmPreviewForm? form;
        lock (Sync)
        {
            form = _active;
        }

        if (form is null || form.IsDisposed)
        {
            return;
        }

        if (form.InvokeRequired)
        {
            form.BeginInvoke(form.ToggleControlPublic);
        }
        else
        {
            form.ToggleControlPublic();
        }
    }

    public static void ReturnActiveToPrimary()
    {
        VmPreviewForm? form;
        lock (Sync)
        {
            form = _active;
        }

        if (form is null || form.IsDisposed)
        {
            // Önizleme kapalı olsa bile imleci primary'ye çek
            MoveCursorToPrimary();
            return;
        }

        if (form.InvokeRequired)
        {
            form.BeginInvoke(form.ReturnToPrimaryPublic);
        }
        else
        {
            form.ReturnToPrimaryPublic();
        }
    }

    public static void MoveCursorToPrimary()
    {
        var primary = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        if (primary is null)
        {
            return;
        }

        var x = primary.Bounds.Left + (primary.Bounds.Width / 2);
        var y = primary.Bounds.Top + (primary.Bounds.Height / 2);
        InputInjector.SetCursorScreenPos(x, y);
    }

    public static void ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var fg = GetForegroundWindow();
        if (fg == hwnd)
        {
            return;
        }

        var targetThread = GetWindowThreadProcessId(hwnd, out _);
        var fgThread = GetWindowThreadProcessId(fg, out _);
        if (fgThread != targetThread)
        {
            AttachThreadInput(fgThread, targetThread, true);
        }

        try
        {
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
        }
        finally
        {
            if (fgThread != targetThread)
            {
                AttachThreadInput(fgThread, targetThread, false);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);
}

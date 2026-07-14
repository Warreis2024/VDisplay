using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VDisplay.Tray;

/// <summary>
/// Tek bir gizli pencere ile sistem genelinde F2/F3 — önizleme odağı kaybolsa bile çalışır.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class GlobalHotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyF3 = 1;
    private const int HotkeyF2 = 2;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF2 = 0x71;
    private const uint VkF3 = 0x72;

    private bool _disposed;
    private Action? _onF2;
    private Action? _onF3;

    public GlobalHotkeyWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "VDisplay.Tray.Hotkeys",
            Style = 0
        });
    }

    public void Bind(Action onF2, Action onF3)
    {
        _onF2 = onF2;
        _onF3 = onF3;
        // MOD_NOREPEAT: basılı tutunca spam olmasın
        RegisterHotKey(Handle, HotkeyF3, ModNoRepeat, VkF3);
        RegisterHotKey(Handle, HotkeyF2, ModNoRepeat, VkF2);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            var id = m.WParam.ToInt32();
            if (id == HotkeyF3)
            {
                _onF3?.Invoke();
                return;
            }

            if (id == HotkeyF2)
            {
                _onF2?.Invoke();
                return;
            }
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Handle != IntPtr.Zero)
        {
            UnregisterHotKey(Handle, HotkeyF3);
            UnregisterHotKey(Handle, HotkeyF2);
            DestroyHandle();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

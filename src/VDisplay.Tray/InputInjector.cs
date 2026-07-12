using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal static class InputInjector
{
    private const int InputMouse = 0;
    private const int InputKeyboard = 1;

    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020;
    private const uint MouseMiddleUp = 0x0040;
    private const uint MouseWheel = 0x0800;
    private const uint MouseAbsolute = 0x8000;
    private const uint MouseVirtualDesk = 0x4000;

    private const uint KeyExtended = 0x0001;
    private const uint KeyUp = 0x0002;
    private const uint KeyUnicode = 0x0004;
    private const uint KeyScanCode = 0x0008;

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public static void MoveTo(int screenX, int screenY)
    {
        if (!TryToAbsolute(screenX, screenY, out var absX, out var absY))
        {
            return;
        }

        var input = new Input
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = MouseMove | MouseAbsolute | MouseVirtualDesk
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    public static void MouseButton(MouseButtons button, bool down)
    {
        uint flag = button switch
        {
            MouseButtons.Left => down ? MouseLeftDown : MouseLeftUp,
            MouseButtons.Right => down ? MouseRightDown : MouseRightUp,
            MouseButtons.Middle => down ? MouseMiddleDown : MouseMiddleUp,
            _ => 0
        };

        if (flag == 0)
        {
            return;
        }

        var input = new Input
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MouseInput { dwFlags = flag }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    public static void MouseClick(int screenX, int screenY, MouseButtons button)
    {
        MoveTo(screenX, screenY);
        MouseButton(button, down: true);
        MouseButton(button, down: false);
    }

    public static void MouseWheelDelta(int delta)
    {
        var input = new Input
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    mouseData = (uint)delta,
                    dwFlags = MouseWheel
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    public static void Key(Keys key, bool down)
    {
        var vk = (ushort)(key & Keys.KeyCode);
        if (vk == 0)
        {
            return;
        }

        var scan = (ushort)MapVirtualKey(vk, 0);
        var flags = KeyScanCode | (down ? 0u : KeyUp);
        if (IsExtendedKey(key))
        {
            flags |= KeyExtended;
        }

        var input = new Input
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wVk = 0,
                    wScan = scan,
                    dwFlags = flags
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    public static bool SetCursorScreenPos(int screenX, int screenY) =>
        SetCursorPos(screenX, screenY);

    private static bool TryToAbsolute(int screenX, int screenY, out int absX, out int absY)
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 1 || height <= 1)
        {
            absX = absY = 0;
            return false;
        }

        absX = (int)Math.Round((screenX - left) * 65535.0 / (width - 1));
        absY = (int)Math.Round((screenY - top) * 65535.0 / (height - 1));
        absX = Math.Clamp(absX, 0, 65535);
        absY = Math.Clamp(absY, 0, 65535);
        return true;
    }

    private static bool IsExtendedKey(Keys key) => key is
        Keys.Up or Keys.Down or Keys.Left or Keys.Right
        or Keys.Insert or Keys.Delete or Keys.Home or Keys.End
        or Keys.PageUp or Keys.PageDown
        or Keys.NumLock or Keys.RControlKey or Keys.RMenu
        or Keys.LWin or Keys.RWin or Keys.Apps
        or Keys.Divide or Keys.Enter;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}

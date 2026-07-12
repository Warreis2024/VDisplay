using System.Runtime.InteropServices;
using System.Text;

var test = new SwDeviceTest();
test.Run();

internal sealed class SwDeviceTest
{
    private readonly ManualResetEventSlim _wait = new(false);
    private int _createResult = -1;
    private readonly SwDeviceApi.SW_DEVICE_CREATE_CALLBACK _callback;

    public SwDeviceTest()
    {
        _callback = OnCreated;
    }

    public void Run()
    {
        const string hardwareId = "VDisplayDriver";
        const string instanceId = "VDisplayDriver";

        var hwBytes = Encoding.Unicode.GetBytes("VDisplayDriver\0\0");
        var hwPtr = Marshal.AllocHGlobal(hwBytes.Length);
        Marshal.Copy(hwBytes, 0, hwPtr, hwBytes.Length);

        var createInfo = new SwDeviceApi.SW_DEVICE_CREATE_INFO
        {
            cbSize = Marshal.SizeOf<SwDeviceApi.SW_DEVICE_CREATE_INFO>(),
            pszInstanceId = SwDeviceApi.AllocString(instanceId),
            pszzHardwareIds = hwPtr,
            pszzCompatibleIds = hwPtr,
            pszDeviceDescription = SwDeviceApi.AllocString("VDisplay test"),
            CapabilityFlags =
                SwDeviceApi.SWDeviceCapabilitiesRemovable |
                SwDeviceApi.SWDeviceCapabilitiesSilentInstall |
                SwDeviceApi.SWDeviceCapabilitiesDriverRequired
        };

        var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        var pinned = GCHandle.Alloc(createInfo, GCHandleType.Pinned);
        try
        {
            var hr = SwDeviceApi.SwDeviceCreate(
                hardwareId,
                @"HTREE\ROOT\0",
                pinned.AddrOfPinnedObject(),
                0,
                IntPtr.Zero,
                _callback,
                waitHandle.SafeWaitHandle.DangerousGetHandle(),
                out var handle);

            Console.WriteLine($"SwDeviceCreate hr=0x{hr:X8}, cbSize={createInfo.cbSize}");

            if (hr >= 0)
            {
                if (!waitHandle.WaitOne(15000))
                {
                    Console.WriteLine("Callback timeout");
                }
                else
                {
                    Console.WriteLine($"Callback hr=0x{_createResult:X8}");
                }

                SwDeviceApi.SwDeviceClose(handle);
            }
        }
        finally
        {
            pinned.Free();
            Marshal.FreeHGlobal(hwPtr);
            Marshal.FreeHGlobal(createInfo.pszInstanceId);
            Marshal.FreeHGlobal(createInfo.pszDeviceDescription);
        }
    }

    private void OnCreated(IntPtr h, int hr, IntPtr ctx, string id)
    {
        _createResult = hr;
        Console.WriteLine($"Callback instance={id}");
        _wait.Set();
    }
}

internal static class SwDeviceApi
{
    public const uint SWDeviceCapabilitiesRemovable = 0x00000001;
    public const uint SWDeviceCapabilitiesSilentInstall = 0x00000002;
    public const uint SWDeviceCapabilitiesNoDisplayInUI = 0x00000004;
    public const uint SWDeviceCapabilitiesDriverRequired = 0x00000008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SW_DEVICE_CREATE_INFO
    {
        public int cbSize;
        public IntPtr pszInstanceId;
        public IntPtr pszzHardwareIds;
        public IntPtr pszzCompatibleIds;
        public IntPtr pContainerId;
        public uint CapabilityFlags;
        public IntPtr pszDeviceDescription;
        public IntPtr pszDeviceLocation;
        public IntPtr pSecurityDescriptor;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void SW_DEVICE_CREATE_CALLBACK(
        IntPtr hSwDevice,
        int hrCreateResult,
        IntPtr pContext,
        [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceInstanceId);

    [DllImport("Cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern int SwDeviceCreate(
        string pszEnumeratorName,
        string pszParentInstanceId,
        IntPtr pCreateInfo,
        uint cPropertyCount,
        IntPtr pProperties,
        SW_DEVICE_CREATE_CALLBACK pCallback,
        IntPtr pContext,
        out IntPtr phSwDevice);

    [DllImport("Cfgmgr32.dll", ExactSpelling = true)]
    public static extern void SwDeviceClose(IntPtr hSwDevice);

    public static IntPtr AllocString(string value) => Marshal.StringToHGlobalUni(value);

    public static IntPtr AllocMultiSz(params string[] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
        {
            var bytes = Encoding.Unicode.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        stream.WriteByte(0);
        stream.WriteByte(0);

        var buffer = stream.ToArray();
        var ptr = Marshal.AllocHGlobal(buffer.Length);
        Marshal.Copy(buffer, 0, ptr, buffer.Length);
        return ptr;
    }
}

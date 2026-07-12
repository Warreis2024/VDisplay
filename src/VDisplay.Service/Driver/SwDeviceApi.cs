using System.Runtime.InteropServices;
using System.Text;

namespace VDisplay.Service.Driver;

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
        ref SW_DEVICE_CREATE_INFO pCreateInfo,
        uint cPropertyCount,
        IntPtr pProperties,
        SW_DEVICE_CREATE_CALLBACK pCallback,
        IntPtr pContext,
        out IntPtr phSwDevice);

    [DllImport("Cfgmgr32.dll", ExactSpelling = true)]
    public static extern void SwDeviceClose(IntPtr hSwDevice);

    public static IntPtr AllocString(string value) =>
        Marshal.StringToHGlobalUni(value);

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

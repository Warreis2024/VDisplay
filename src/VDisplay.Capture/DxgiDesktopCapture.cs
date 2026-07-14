using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VDisplay.Core.Models;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using DxgiResult = Vortice.DXGI.ResultCode;

namespace VDisplay.Capture;

[SupportedOSPlatform("windows")]
public static class DxgiDesktopCapture
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, OutputSession> Sessions = new(StringComparer.Ordinal);

    public static Bitmap? TryCapture(PhysicalMonitorInfo monitor, int maxWidth = 0, int maxHeight = 0)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0)
        {
            return null;
        }

        var key = SessionKey(monitor);
        lock (Gate)
        {
            if (!TryGetSession(key, monitor, out var session) || session is null)
            {
                return null;
            }

            return session.Capture(maxWidth, maxHeight);
        }
    }

    /// <summary>
    /// Staging texture → BGRA bytes (ara Bitmap yok). buffer gerekirse yeniden boyutlanır.
    /// </summary>
    public static bool TryCaptureBgra(
        PhysicalMonitorInfo monitor,
        ref byte[]? buffer,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        if (monitor.Width <= 0 || monitor.Height <= 0)
        {
            return false;
        }

        var key = SessionKey(monitor);
        lock (Gate)
        {
            if (!TryGetSession(key, monitor, out var session) || session is null)
            {
                return false;
            }

            return session.CaptureBgra(ref buffer, out width, out height);
        }
    }

    private static string SessionKey(PhysicalMonitorInfo monitor) =>
        $"{monitor.X},{monitor.Y},{monitor.Width},{monitor.Height}";

    private static bool TryGetSession(string key, PhysicalMonitorInfo monitor, out OutputSession? session)
    {
        if (!Sessions.TryGetValue(key, out session) || session.NeedsRecreate)
        {
            session?.Dispose();
            session = OutputSession.TryCreate(monitor);
            if (session is null)
            {
                Sessions.Remove(key);
                return false;
            }

            Sessions[key] = session;
        }

        return true;
    }

    public static void InvalidateAll()
    {
        lock (Gate)
        {
            foreach (var session in Sessions.Values)
            {
                session.Dispose();
            }

            Sessions.Clear();
        }
    }

    private sealed class OutputSession : IDisposable
    {
        private readonly PhysicalMonitorInfo _monitor;
        private readonly IDXGIAdapter1 _adapter;
        private readonly IDXGIOutput1 _output;
        private readonly ID3D11Device _device;
        private readonly IDXGIOutputDuplication _duplication;
        private readonly ID3D11Texture2D _staging;
        private readonly int _width;
        private readonly int _height;
        private byte[]? _bgraBuffer;

        private static readonly FeatureLevel[] FeatureLevels =
        [
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        ];

        public bool NeedsRecreate { get; private set; }

        private OutputSession(
            PhysicalMonitorInfo monitor,
            IDXGIAdapter1 adapter,
            IDXGIOutput1 output,
            ID3D11Device device,
            IDXGIOutputDuplication duplication,
            ID3D11Texture2D staging,
            int width,
            int height)
        {
            _monitor = monitor;
            _adapter = adapter;
            _output = output;
            _device = device;
            _duplication = duplication;
            _staging = staging;
            _width = width;
            _height = height;
        }

        public static OutputSession? TryCreate(PhysicalMonitorInfo monitor)
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out var adapter).Success; adapterIndex++)
            {
                var adapter1 = adapter.QueryInterface<IDXGIAdapter1>();

                for (uint outputIndex = 0; adapter1.EnumOutputs(outputIndex, out var output).Success; outputIndex++)
                {
                    var output1 = output.QueryInterface<IDXGIOutput1>();
                    var desc = output1.Description;
                    var rect = desc.DesktopCoordinates;
                    var width = rect.Right - rect.Left;
                    var height = rect.Bottom - rect.Top;

                    if (rect.Left != monitor.X || rect.Top != monitor.Y
                        || width != monitor.Width || height != monitor.Height)
                    {
                        output1.Dispose();
                        output.Dispose();
                        continue;
                    }

                    var featureLevels = FeatureLevels;
                    var result = D3D11.D3D11CreateDevice(
                        adapter1,
                        DriverType.Unknown,
                        DeviceCreationFlags.BgraSupport,
                        featureLevels,
                        out var device);

                    if (result.Failure || device is null)
                    {
                        output1.Dispose();
                        output.Dispose();
                        continue;
                    }

                    IDXGIOutputDuplication duplication;
                    try
                    {
                        duplication = output1.DuplicateOutput(device);
                    }
                    catch
                    {
                        device.Dispose();
                        output1.Dispose();
                        output.Dispose();
                        continue;
                    }

                    var stagingDesc = new Texture2DDescription
                    {
                        CPUAccessFlags = CpuAccessFlags.Read,
                        BindFlags = BindFlags.None,
                        Format = Format.B8G8R8A8_UNorm,
                        Width = (uint)width,
                        Height = (uint)height,
                        MipLevels = 1,
                        ArraySize = 1,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Staging
                    };

                    var staging = device.CreateTexture2D(stagingDesc);
                    if (staging is null)
                    {
                        duplication.Dispose();
                        device.Dispose();
                        output1.Dispose();
                        output.Dispose();
                        continue;
                    }

                    output.Dispose();
                    return new OutputSession(
                        monitor,
                        adapter1,
                        output1,
                        device,
                        duplication,
                        staging,
                        width,
                        height);
                }

                adapter1.Dispose();
                adapter.Dispose();
            }

            return null;
        }

        public Bitmap? Capture(int maxWidth, int maxHeight)
        {
            if (!CaptureBgra(ref _bgraBuffer, out var width, out var height) || _bgraBuffer is null)
            {
                return null;
            }

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                var rowBytes = width * 4;
                if (bmpData.Stride == rowBytes)
                {
                    Marshal.Copy(_bgraBuffer, 0, bmpData.Scan0, rowBytes * height);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Marshal.Copy(_bgraBuffer, y * rowBytes, bmpData.Scan0 + (y * bmpData.Stride), rowBytes);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return ScaleIfNeeded(bitmap, maxWidth, maxHeight);
        }

        public bool CaptureBgra(ref byte[]? buffer, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (NeedsRecreate)
            {
                return false;
            }

            var acquired = false;
            IDXGIResource? desktopResource = null;
            try
            {
                var result = _duplication.AcquireNextFrame(100, out _, out desktopResource);
                if (result == DxgiResult.WaitTimeout)
                {
                    return false;
                }

                if (result == DxgiResult.AccessLost || result.Failure)
                {
                    NeedsRecreate = true;
                    return false;
                }

                acquired = true;
                using var desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
                _device.ImmediateContext.CopyResource(_staging, desktopTexture);

                var mapped = _device.ImmediateContext.Map(_staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    width = _width;
                    height = _height;
                    var rowBytes = width * 4;
                    var byteCount = rowBytes * height;
                    if (buffer is null || buffer.Length < byteCount)
                    {
                        buffer = new byte[byteCount];
                    }

                    var srcPitch = (int)mapped.RowPitch;
                    for (var y = 0; y < height; y++)
                    {
                        Marshal.Copy(
                            mapped.DataPointer + (y * srcPitch),
                            buffer,
                            y * rowBytes,
                            rowBytes);
                    }

                    for (var i = 3; i < byteCount; i += 4)
                    {
                        buffer[i] = 255;
                    }

                    return true;
                }
                finally
                {
                    _device.ImmediateContext.Unmap(_staging, 0);
                }
            }
            finally
            {
                if (acquired)
                {
                    _duplication.ReleaseFrame();
                }
            }
        }

        private static Bitmap ScaleIfNeeded(Bitmap source, int maxWidth, int maxHeight)
        {
            var targetW = source.Width;
            var targetH = source.Height;

            if (maxWidth > 0 && targetW > maxWidth)
            {
                targetW = maxWidth;
                targetH = Math.Max(1, (int)((double)source.Height * maxWidth / source.Width));
            }

            if (maxHeight > 0 && targetH > maxHeight)
            {
                targetH = maxHeight;
                targetW = Math.Max(1, (int)((double)source.Width * maxHeight / source.Height));
            }

            if (targetW == source.Width && targetH == source.Height)
            {
                return source;
            }

            var scaled = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                g.DrawImage(source, 0, 0, targetW, targetH);
            }

            source.Dispose();
            return scaled;
        }

        public void Dispose()
        {
            _staging.Dispose();
            _duplication.Dispose();
            _device.Dispose();
            _output.Dispose();
            _adapter.Dispose();
        }
    }
}

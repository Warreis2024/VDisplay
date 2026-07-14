using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using VDisplay.Core.Interop;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal sealed class D3DPreviewPanel : Control
{
    private const string ShaderSource = """
cbuffer Params : register(b0)
{
    float4 UvRect;
    float4 NdcRect;
};

Texture2D SrcTex : register(t0);
SamplerState Samp : register(s0);

struct VSOut
{
    float4 Pos : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

VSOut VSMain(uint id : SV_VertexID)
{
    float2 corner = float2((id & 1), (id >> 1));
    VSOut o;
    o.Pos = float4(
        lerp(NdcRect.x, NdcRect.z, corner.x),
        lerp(NdcRect.y, NdcRect.w, corner.y),
        0, 1);
    o.Uv = float2(
        lerp(UvRect.x, UvRect.z, corner.x),
        lerp(UvRect.y, UvRect.w, corner.y));
    return o;
}

float4 PSMain(VSOut i) : SV_TARGET
{
    return SrcTex.Sample(Samp, i.Uv);
}
""";

    private readonly int _vmIndex;
    private readonly SharedGpuFrameReader _gpuReader;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11Device1? _device1;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _rtv;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11Buffer? _cb;
    private ID3D11SamplerState? _sampler;
    private ID3D11Texture2D? _sharedTex;
    private ID3D11ShaderResourceView? _srv;
    private IntPtr _localShareHandle;
    private long _boundSequence = -1;
    private long _boundSourceHandle;
    private long _boundLuid;
    private bool _gpuReady;
    private int _backW;
    private int _backH;

    public Rectangle ContentBounds { get; private set; }
    public bool IsGpuPathActive => _gpuReady;

    public D3DPreviewPanel(int vmIndex, SharedGpuFrameReader gpuReader)
    {
        _vmIndex = vmIndex;
        _gpuReader = gpuReader;
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = System.Drawing.Color.Black;
        TabStop = true;
    }

    public bool TryStart()
    {
        if (!IsHandleCreated)
        {
            CreateHandle();
        }

        try
        {
            return EnsureDeviceForDefaultAdapter() && EnsurePipeline() && EnsureSwapChain();
        }
        catch
        {
            DisposeGpu();
            return false;
        }
    }

    public bool TryPresent()
    {
        if (!_gpuReady || _device is null || _context is null || _swapChain is null || _rtv is null)
        {
            return false;
        }

        try
        {
            if (!EnsureSize())
            {
                return false;
            }

            if (!_gpuReader.TryReadVmRegion(_vmIndex, out var region, out _, out _))
            {
                return false;
            }

            if (!_gpuReader.TryReadSourceSlot(region.SourceMonitorIndex, out var slot))
            {
                return false;
            }

            if (!EnsureSharedTexture(slot))
            {
                return false;
            }

            UpdateContentBounds(region.SrcW, region.SrcH);
            UpdateConstants(region, slot.Width, slot.Height);

            _context.ClearRenderTargetView(_rtv, new Color4(0, 0, 0, 1));
            _context.OMSetRenderTargets(_rtv);
            _context.RSSetViewport(new Viewport(Width, Height));
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _context.VSSetShader(_vs);
            _context.PSSetShader(_ps);
            if (_srv is not null)
            {
                _context.PSSetShaderResource(0, _srv);
            }

            _context.PSSetSampler(0, _sampler);
            _context.VSSetConstantBuffer(0, _cb);
            _context.PSSetConstantBuffer(0, _cb);
            _context.Draw(4, 0);
            _context.PSSetShaderResource(0, null!);

            return !_swapChain.Present(0, PresentFlags.None).Failure;
        }
        catch
        {
            _gpuReady = false;
            return false;
        }
    }

    private bool EnsureSharedTexture(SharedGpuSourceSlot slot)
    {
        if (_sharedTex is not null
            && _boundSequence == slot.Sequence
            && _boundSourceHandle == slot.SharedHandle)
        {
            return _srv is not null;
        }

        if (_boundLuid != 0 && _boundLuid != slot.AdapterLuid)
        {
            DisposeGpuKeepPanel();
            if (!EnsureDeviceForLuid(slot.AdapterLuid) || !EnsurePipeline() || !EnsureSwapChain())
            {
                return false;
            }
        }
        else if (_device is null)
        {
            if (!EnsureDeviceForLuid(slot.AdapterLuid) || !EnsurePipeline() || !EnsureSwapChain())
            {
                return false;
            }
        }

        ReleaseSharedView();

        var remote = new IntPtr(slot.SharedHandle);
        if (remote == IntPtr.Zero || slot.ProducerProcessId <= 0)
        {
            return false;
        }

        if (!TryDuplicateHandle(slot.ProducerProcessId, remote, out var local))
        {
            return false;
        }

        _localShareHandle = local;
        _device1 ??= _device!.QueryInterface<ID3D11Device1>();
        try
        {
            _sharedTex = _device1.OpenSharedResource1<ID3D11Texture2D>(local);
        }
        catch
        {
            CloseHandle(local);
            _localShareHandle = IntPtr.Zero;
            return false;
        }

        _srv = _device!.CreateShaderResourceView(_sharedTex);
        _boundSequence = slot.Sequence;
        _boundSourceHandle = slot.SharedHandle;
        _boundLuid = slot.AdapterLuid;
        _gpuReady = true;
        return _srv is not null;
    }

    private static bool TryDuplicateHandle(int producerPid, IntPtr remoteHandle, out IntPtr localHandle)
    {
        localHandle = IntPtr.Zero;
        var process = OpenProcess(PROCESS_DUP_HANDLE, false, producerPid);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!DuplicateHandle(
                    process,
                    remoteHandle,
                    GetCurrentProcess(),
                    out localHandle,
                    0,
                    false,
                    2)) // DUPLICATE_SAME_ACCESS
            {
                localHandle = IntPtr.Zero;
                return false;
            }

            return localHandle != IntPtr.Zero;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private bool EnsureDeviceForDefaultAdapter()
    {
        var result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
            out _device,
            out _,
            out _context);
        _gpuReady = result.Success && _device is not null && _context is not null;
        return _gpuReady;
    }

    private bool EnsureDeviceForLuid(long adapterLuid)
    {
        DisposeGpuKeepPanel();

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
        {
            using (adapter)
            {
                var desc = adapter.Description1;
                var luid = ((long)(uint)desc.Luid.HighPart << 32) | (uint)desc.Luid.LowPart;
                if (luid != adapterLuid)
                {
                    continue;
                }

                var result = D3D11.D3D11CreateDevice(
                    adapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.BgraSupport,
                    [FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0],
                    out _device,
                    out _,
                    out _context);
                _boundLuid = adapterLuid;
                _gpuReady = result.Success && _device is not null && _context is not null;
                return _gpuReady;
            }
        }

        return EnsureDeviceForDefaultAdapter();
    }

    private bool EnsurePipeline()
    {
        if (_vs is not null)
        {
            return true;
        }

        var path = Path.Combine(Path.GetTempPath(), $"vdisplay_preview_{Environment.ProcessId}.hlsl");
        File.WriteAllText(path, ShaderSource, Encoding.ASCII);
        Blob? vsBlob = null;
        Blob? psBlob = null;
        Blob? err = null;
        try
        {
            var vsHr = Compiler.CompileFromFile(
                path,
                null,
                null,
                "VSMain",
                "vs_4_0",
                ShaderFlags.OptimizationLevel3,
                EffectFlags.None,
                out vsBlob,
                out err);
            if (vsHr.Failure)
            {
                return false;
            }

            err?.Dispose();
            err = null;
            var psHr = Compiler.CompileFromFile(
                path,
                null,
                null,
                "PSMain",
                "ps_4_0",
                ShaderFlags.OptimizationLevel3,
                EffectFlags.None,
                out psBlob,
                out err);
            if (psHr.Failure)
            {
                return false;
            }

            _vs = _device!.CreateVertexShader(vsBlob!);
            _ps = _device.CreatePixelShader(psBlob!);
            _cb = _device.CreateBuffer(new BufferDescription
            {
                ByteWidth = 32,
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CPUAccessFlags = CpuAccessFlags.Write
            });
            _sampler = _device.CreateSamplerState(new SamplerDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MaxLOD = float.MaxValue
            });
            return true;
        }
        finally
        {
            vsBlob?.Dispose();
            psBlob?.Dispose();
            err?.Dispose();
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    private bool EnsureSwapChain()
    {
        if (_swapChain is not null)
        {
            return true;
        }

        using var dxgiDevice = _device!.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();

        var desc = new SwapChainDescription1
        {
            Width = (uint)Math.Max(1, ClientSize.Width),
            Height = (uint)Math.Max(1, ClientSize.Height),
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
            Flags = SwapChainFlags.None
        };

        _swapChain = factory.CreateSwapChainForHwnd(_device, Handle, desc);
        factory.MakeWindowAssociation(Handle, WindowAssociationFlags.IgnoreAll);
        return RecreateRtv();
    }

    private bool EnsureSize()
    {
        var w = Math.Max(1, ClientSize.Width);
        var h = Math.Max(1, ClientSize.Height);
        if (_swapChain is null)
        {
            return EnsureSwapChain();
        }

        if (w == _backW && h == _backH)
        {
            return true;
        }

        _rtv?.Dispose();
        _rtv = null;
        if (_swapChain.ResizeBuffers(2, (uint)w, (uint)h, Format.B8G8R8A8_UNorm, SwapChainFlags.None).Failure)
        {
            return false;
        }

        return RecreateRtv();
    }

    private bool RecreateRtv()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _rtv = _device!.CreateRenderTargetView(backBuffer);
        var desc = backBuffer.Description;
        _backW = (int)desc.Width;
        _backH = (int)desc.Height;
        return _rtv is not null;
    }

    private void UpdateContentBounds(uint srcW, uint srcH)
    {
        if (srcW == 0 || srcH == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            ContentBounds = ClientRectangle;
            return;
        }

        var scale = Math.Min(ClientSize.Width / (float)srcW, ClientSize.Height / (float)srcH);
        var dispW = Math.Max(1, (int)(srcW * scale));
        var dispH = Math.Max(1, (int)(srcH * scale));
        var x = (ClientSize.Width - dispW) / 2;
        var y = (ClientSize.Height - dispH) / 2;
        ContentBounds = new Rectangle(x, y, dispW, dispH);
    }

    private void UpdateConstants(SharedVirtualRegion region, int texW, int texH)
    {
        if (_cb is null || _context is null || texW <= 0 || texH <= 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        var u0 = region.SrcX / (float)texW;
        var v0 = region.SrcY / (float)texH;
        var u1 = (region.SrcX + region.SrcW) / (float)texW;
        var v1 = (region.SrcY + region.SrcH) / (float)texH;

        var bounds = ContentBounds;
        float ToNdcX(int px) => (px / (float)ClientSize.Width) * 2f - 1f;
        float ToNdcY(int py) => 1f - (py / (float)ClientSize.Height) * 2f;

        var x0 = ToNdcX(bounds.Left);
        var x1 = ToNdcX(bounds.Right);
        var ndcTop = ToNdcY(bounds.Top);
        var ndcBottom = ToNdcY(bounds.Bottom);

        var data = new float[] { u0, v0, u1, v1, x0, ndcTop, x1, ndcBottom };
        var mapped = _context.Map(_cb, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            Marshal.Copy(data, 0, mapped.DataPointer, 8);
        }
        finally
        {
            _context.Unmap(_cb, 0);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_gpuReady)
        {
            e.Graphics.Clear(System.Drawing.Color.Black);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeGpu();
        }

        base.Dispose(disposing);
    }

    private void ReleaseSharedView()
    {
        _srv?.Dispose();
        _srv = null;
        _sharedTex?.Dispose();
        _sharedTex = null;
        if (_localShareHandle != IntPtr.Zero)
        {
            CloseHandle(_localShareHandle);
            _localShareHandle = IntPtr.Zero;
        }

        _boundSequence = -1;
        _boundSourceHandle = 0;
    }

    private void DisposeGpuKeepPanel()
    {
        ReleaseSharedView();
        _rtv?.Dispose();
        _rtv = null;
        _swapChain?.Dispose();
        _swapChain = null;
        _sampler?.Dispose();
        _sampler = null;
        _cb?.Dispose();
        _cb = null;
        _ps?.Dispose();
        _ps = null;
        _vs?.Dispose();
        _vs = null;
        _device1?.Dispose();
        _device1 = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
        _boundLuid = 0;
        _gpuReady = false;
        _backW = 0;
        _backH = 0;
    }

    private void DisposeGpu() => DisposeGpuKeepPanel();

    private const uint PROCESS_DUP_HANDLE = 0x0040;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        IntPtr hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr lpTargetHandle,
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwOptions);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}

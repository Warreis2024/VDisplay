using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using VDisplay.Core.Models;

namespace VDisplay.Service.Compositor;

[SupportedOSPlatform("windows")]
internal static class DisplayCaptureHelper
{
    public static Bitmap? Capture(PhysicalMonitorInfo monitor) =>
        GdiScreenCapture.CaptureMonitor(monitor);

    public static Bitmap? CaptureLeftHalf(PhysicalMonitorInfo monitor) =>
        GdiScreenCapture.CaptureLeftHalf(monitor);

    public static Bitmap? CaptureRightHalf(PhysicalMonitorInfo monitor) =>
        GdiScreenCapture.CaptureRightHalf(monitor);

    public static Bitmap? CaptureBehindOverlays(
        PhysicalMonitorInfo monitor,
        IEnumerable<Form> overlays,
        bool rightHalf = false,
        PhysicalMonitorInfo? vmFallback = null)
    {
        using var scope = new OverlayHideScope(overlays);
        scope.Hide();

        if (vmFallback is not null)
        {
            var vmBitmap = Capture(vmFallback);
            if (vmBitmap is not null && !IsMostlyBlack(vmBitmap))
            {
                return vmBitmap;
            }

            vmBitmap?.Dispose();
        }

        return rightHalf
            ? CaptureRightHalf(monitor)
            : CaptureLeftHalf(monitor);
    }

    public static bool IsMostlyBlack(Bitmap bitmap)
    {
        var stepX = Math.Max(1, bitmap.Width / 8);
        var stepY = Math.Max(1, bitmap.Height / 8);
        var dark = 0;
        var total = 0;

        for (var y = 0; y < bitmap.Height; y += stepY)
        {
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                var pixel = bitmap.GetPixel(x, y);
                total++;
                if (pixel.R < 8 && pixel.G < 8 && pixel.B < 8)
                {
                    dark++;
                }
            }
        }

        return total > 0 && dark > total * 0.95;
    }
}

[SupportedOSPlatform("windows")]
internal sealed class OverlayHideScope : IDisposable
{
    private readonly List<Form> _forms;
    private readonly List<double> _opacity;

    public OverlayHideScope(IEnumerable<Form> overlays)
    {
        _forms = overlays.Where(f => f is { IsDisposed: false }).Distinct().ToList();
        _opacity = _forms.Select(f => f.Opacity).ToList();
    }

    public void Hide()
    {
        foreach (var form in _forms)
        {
            form.Opacity = 0;
            form.Hide();
        }

        Application.DoEvents();
        Thread.Sleep(40);
    }

    public void Dispose()
    {
        for (var i = 0; i < _forms.Count; i++)
        {
            var form = _forms[i];
            if (form.IsDisposed)
            {
                continue;
            }

            form.Opacity = _opacity[i];
            form.Show();
        }
    }
}

using VDisplay.Core.Models;

namespace VDisplay.Core.Layout;

public static class LayoutPresetBuilder
{
    public static LayoutDefinition Build(LayoutType layoutType, int sourceWidth, int sourceHeight)
    {
        return layoutType switch
        {
            LayoutType.TwoVertical => BuildTwoVertical(sourceWidth, sourceHeight),
            LayoutType.TwoHorizontal => BuildTwoHorizontal(sourceWidth, sourceHeight),
            LayoutType.ThreeVertical => BuildThreeVertical(sourceWidth, sourceHeight),
            LayoutType.FourGrid => BuildFourGrid(sourceWidth, sourceHeight),
            _ => BuildTwoVertical(sourceWidth, sourceHeight)
        };
    }

    public static LayoutType DefaultForCount(int count) => count switch
    {
        1 => LayoutType.Custom,
        2 => LayoutType.TwoVertical,
        3 => LayoutType.ThreeVertical,
        4 => LayoutType.FourGrid,
        _ => LayoutType.Custom
    };

    public static LayoutDefinition BuildForCount(int count, int sourceWidth, int sourceHeight)
    {
        count = Math.Clamp(count, 1, 10);
        return count switch
        {
            1 => BuildSingle(sourceWidth, sourceHeight),
            2 => BuildTwoVertical(sourceWidth, sourceHeight),
            3 => BuildThreeVertical(sourceWidth, sourceHeight),
            4 => BuildFourGrid(sourceWidth, sourceHeight),
            _ => BuildNColumns(count, sourceWidth, sourceHeight)
        };
    }

    public static void ApplyVirtualMonitorSizes(LayoutDefinition layout, IReadOnlyList<PhysicalMonitorInfo> virtualMonitors)
    {
        if (virtualMonitors.Count == 0)
        {
            return;
        }

        for (var i = 0; i < layout.Regions.Count && i < virtualMonitors.Count; i++)
        {
            var vm = virtualMonitors[i];
            if (vm.Width <= 0 || vm.Height <= 0)
            {
                continue;
            }

            layout.Regions[i].Destination.Width = vm.Width;
            layout.Regions[i].Destination.Height = vm.Height;
        }
    }

    private static LayoutDefinition BuildSingle(int w, int h)
    {
        return new LayoutDefinition
        {
            LayoutType = LayoutType.Custom,
            SourceWidth = w,
            SourceHeight = h,
            Regions = [Region(0, 0, 0, w, h, w, h)]
        };
    }

    private static LayoutDefinition BuildNColumns(int count, int w, int h)
    {
        var regions = new List<VirtualRegion>(count);
        var baseW = w / count;
        var used = 0;
        for (var i = 0; i < count; i++)
        {
            var colW = i == count - 1 ? w - used : baseW;
            regions.Add(Region(i, used, 0, colW, h, colW, h));
            used += colW;
        }

        return new LayoutDefinition
        {
            LayoutType = LayoutType.Custom,
            SourceWidth = w,
            SourceHeight = h,
            Regions = regions
        };
    }

    private static LayoutDefinition BuildTwoVertical(int w, int h)
    {
        var half = w / 2;
        return new LayoutDefinition
        {
            LayoutType = LayoutType.TwoVertical,
            SourceWidth = w,
            SourceHeight = h,
            Regions =
            [
                Region(0, 0, 0, half, h, half, h),
                Region(1, half, 0, w - half, h, w - half, h)
            ]
        };
    }

    private static LayoutDefinition BuildTwoHorizontal(int w, int h)
    {
        var half = h / 2;
        return new LayoutDefinition
        {
            LayoutType = LayoutType.TwoHorizontal,
            SourceWidth = w,
            SourceHeight = h,
            Regions =
            [
                Region(0, 0, 0, w, half, w, half),
                Region(1, 0, half, w, h - half, w, h - half)
            ]
        };
    }

    private static LayoutDefinition BuildThreeVertical(int w, int h)
    {
        var third = w / 3;
        var last = w - (third * 2);
        return new LayoutDefinition
        {
            LayoutType = LayoutType.ThreeVertical,
            SourceWidth = w,
            SourceHeight = h,
            Regions =
            [
                Region(0, 0, 0, third, h, third, h),
                Region(1, third, 0, third, h, third, h),
                Region(2, third * 2, 0, last, h, last, h)
            ]
        };
    }

    private static LayoutDefinition BuildFourGrid(int w, int h)
    {
        var halfW = w / 2;
        var halfH = h / 2;
        return new LayoutDefinition
        {
            LayoutType = LayoutType.FourGrid,
            SourceWidth = w,
            SourceHeight = h,
            Regions =
            [
                Region(0, 0, 0, halfW, halfH, halfW, halfH),
                Region(1, halfW, 0, w - halfW, halfH, w - halfW, halfH),
                Region(2, 0, halfH, halfW, h - halfH, halfW, h - halfH),
                Region(3, halfW, halfH, w - halfW, h - halfH, w - halfW, h - halfH)
            ]
        };
    }

    public static LayoutDefinition BuildDualPhysicalTwoVertical(IReadOnlyList<PhysicalMonitorInfo> sources)
    {
        var regions = new List<VirtualRegion>();
        foreach (var source in sources)
        {
            if (regions.Count >= 4)
            {
                break;
            }

            var half = source.Width / 2;
            var rightW = source.Width - half;
            var vmIndex = regions.Count;

            regions.Add(Region(vmIndex, 0, 0, half, source.Height, half, source.Height, source.Index));
            regions.Add(Region(vmIndex + 1, half, 0, rightW, source.Height, rightW, source.Height, source.Index));

            if (regions.Count >= 4)
            {
                break;
            }
        }

        var first = sources[0];
        return new LayoutDefinition
        {
            LayoutType = LayoutType.Custom,
            SourceWidth = first.Width,
            SourceHeight = first.Height,
            Regions = regions
        };
    }

    private static VirtualRegion Region(
        int index,
        int x,
        int y,
        int width,
        int height,
        int dstW,
        int dstH,
        int sourceMonitorIndex = 0) =>
        new()
        {
            Index = index,
            SourceMonitorIndex = sourceMonitorIndex,
            Source = new RegionRect { X = x, Y = y, Width = width, Height = height },
            Destination = new RegionRect { X = 0, Y = 0, Width = dstW, Height = dstH }
        };
}

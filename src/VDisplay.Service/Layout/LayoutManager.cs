using System.Text.Json;
using VDisplay.Core;
using VDisplay.Core.Layout;
using VDisplay.Core.Models;

namespace VDisplay.Service.Layout;

public sealed class LayoutManager
{
    private readonly object _sync = new();
    private LayoutType _layoutType = LayoutType.TwoVertical;
    private LayoutDefinition? _activeLayout;
    private int _sourceMonitorIndex;

    public LayoutDefinition ApplyLayout(LayoutType layoutType, int monitorCount, PhysicalMonitorInfo sourceMonitor)
    {
        lock (_sync)
        {
            _layoutType = layoutType;
            _sourceMonitorIndex = sourceMonitor.Index;
            _activeLayout = LayoutPresetBuilder.Build(layoutType, sourceMonitor.Width, sourceMonitor.Height);

            if (_activeLayout.Regions.Count > monitorCount)
            {
                _activeLayout.Regions = _activeLayout.Regions.Take(monitorCount).ToList();
            }

            return _activeLayout;
        }
    }

    public LayoutDefinition ApplyLayoutForCount(int monitorCount, PhysicalMonitorInfo sourceMonitor)
    {
        lock (_sync)
        {
            _layoutType = LayoutPresetBuilder.DefaultForCount(monitorCount);
            _sourceMonitorIndex = sourceMonitor.Index;
            _activeLayout = LayoutPresetBuilder.BuildForCount(monitorCount, sourceMonitor.Width, sourceMonitor.Height);
            return _activeLayout;
        }
    }

    public void SetLayout(LayoutType layoutType, int monitorCount, PhysicalMonitorInfo sourceMonitor)
    {
        ApplyLayout(layoutType, monitorCount, sourceMonitor);
    }

    public void SetSourceMonitorIndex(int index)
    {
        lock (_sync)
        {
            _sourceMonitorIndex = index;
        }
    }

    public (LayoutType LayoutType, LayoutDefinition? Layout, int SourceMonitorIndex) GetSnapshot()
    {
        lock (_sync)
        {
            return (_layoutType, _activeLayout, _sourceMonitorIndex);
        }
    }

    public static LayoutType? ParseLayoutType(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("layout", out var layoutElement)
            && Enum.TryParse<LayoutType>(layoutElement.GetString(), ignoreCase: true, out var layout))
        {
            return layout;
        }

        return null;
    }

    public static int? ParseSourceMonitorIndex(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var index))
        {
            return index;
        }

        return null;
    }
}

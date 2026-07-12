using System.Text.Json;
using VDisplay.Core;
using VDisplay.Core.Layout;
using VDisplay.Core.Models;
using VDisplay.Service.Capture;
using VDisplay.Service.Layout;

namespace VDisplay.Service.Monitor;

public sealed class MonitorManager
{
    private readonly LayoutManager _layoutManager;
    private readonly PhysicalMonitorProvider _physicalMonitorProvider;
    private readonly object _sync = new();
    private int _monitorCount;
    private LayoutType _layout = LayoutType.TwoVertical;
    private LayoutDefinition? _activeLayout;
    private int _sourceMonitorIndex;

    public MonitorManager(LayoutManager layoutManager, PhysicalMonitorProvider physicalMonitorProvider)
    {
        _layoutManager = layoutManager;
        _physicalMonitorProvider = physicalMonitorProvider;
    }

    public int MonitorCount
    {
        get
        {
            lock (_sync)
            {
                return _monitorCount;
            }
        }
    }

    public int SourceMonitorIndex
    {
        get
        {
            lock (_sync)
            {
                return _sourceMonitorIndex;
            }
        }
    }

    public LayoutDefinition? GetActiveLayout()
    {
        lock (_sync)
        {
            return _activeLayout;
        }
    }

    public void SetMonitorCount(int count)
    {
        if (count is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Monitör sayısı 1-4 arasında olmalı.");
        }

        lock (_sync)
        {
            _monitorCount = count;
            _layout = LayoutPresetBuilder.DefaultForCount(count);
            RefreshLayoutLocked();
        }
    }

    public void SetLayout(LayoutType layoutType)
    {
        lock (_sync)
        {
            if (_monitorCount == 0)
            {
                _monitorCount = LayoutRegionCount(layoutType);
            }

            _layout = layoutType;
            _monitorCount = LayoutRegionCount(layoutType);
            RefreshLayoutLocked();
        }
    }

    public void SetSourceMonitorIndex(int index)
    {
        lock (_sync)
        {
            _sourceMonitorIndex = index;
            _layoutManager.SetSourceMonitorIndex(index);
            RefreshLayoutLocked();
        }
    }

    public void ApplyDualPhysicalSplit(IReadOnlyList<PhysicalMonitorInfo> physicalMonitors)
    {
        if (physicalMonitors.Count == 0)
        {
            throw new InvalidOperationException("Fiziksel monitör bulunamadı.");
        }

        lock (_sync)
        {
            _layout = LayoutType.Custom;
            _monitorCount = Math.Min(4, physicalMonitors.Count * 2);
            _activeLayout = LayoutPresetBuilder.BuildDualPhysicalTwoVertical(physicalMonitors);
            _sourceMonitorIndex = physicalMonitors[0].Index;
            _layoutManager.SetSourceMonitorIndex(_sourceMonitorIndex);
            RemapLayoutToVirtualDisplaysLocked();
        }
    }

    public void ApplyPrimarySplit(PhysicalMonitorInfo sourceMonitor)
    {
        lock (_sync)
        {
            _monitorCount = 2;
            _layout = LayoutType.TwoVertical;
            _sourceMonitorIndex = sourceMonitor.Index;
            _layoutManager.SetSourceMonitorIndex(_sourceMonitorIndex);
            _activeLayout = _layoutManager.ApplyLayout(_layout, _monitorCount, sourceMonitor);
            RemapLayoutToVirtualDisplaysLocked();
        }
    }

    public void RefreshVirtualMonitorMapping()
    {
        lock (_sync)
        {
            RemapLayoutToVirtualDisplaysLocked();
        }
    }

    public ServiceStatus GetStatus(
        bool driverRunning,
        bool captureRunning,
        bool physicalSplitRunning,
        ulong framesCaptured,
        ulong framesRendered)
    {
        lock (_sync)
        {
            var monitors = BuildMonitorList(driverRunning);

            return new ServiceStatus
            {
                DriverRunning = driverRunning,
                CaptureRunning = captureRunning,
                PhysicalSplitRunning = physicalSplitRunning,
                MonitorCount = _monitorCount,
                SourceMonitorIndex = _sourceMonitorIndex,
                CurrentLayout = _layout,
                ActiveLayout = _activeLayout,
                Monitors = monitors,
                FramesCaptured = framesCaptured + framesRendered
            };
        }
    }

    private void RefreshLayoutLocked()
    {
        var source = ResolveSourceMonitor();
        if (source is null)
        {
            _activeLayout = null;
            return;
        }

        _sourceMonitorIndex = source.Index;
        _activeLayout = _layoutManager.ApplyLayout(_layout, _monitorCount, source);
        RemapLayoutToVirtualDisplaysLocked();
    }

    private void RemapLayoutToVirtualDisplaysLocked()
    {
        if (_activeLayout is null)
        {
            return;
        }

        var vms = _physicalMonitorProvider.GetVirtualMonitorDisplays(_monitorCount);
        LayoutPresetBuilder.ApplyVirtualMonitorSizes(_activeLayout, vms);
    }

    private PhysicalMonitorInfo? ResolveSourceMonitor()
    {
        var monitors = _physicalMonitorProvider.GetCaptureSources();
        return monitors.FirstOrDefault(m => m.Index == _sourceMonitorIndex)
            ?? monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors.FirstOrDefault();
    }

    private List<VirtualMonitorInfo> BuildMonitorList(bool driverRunning)
    {
        if (_activeLayout is null || _activeLayout.Regions.Count == 0)
        {
            return [];
        }

        return _activeLayout.Regions.Select(region => new VirtualMonitorInfo
        {
            Index = region.Index,
            Width = region.Destination.Width,
            Height = region.Destination.Height,
            RefreshRate = 60,
            Name = $"VDisplay VM {region.Index + 1}",
            IsActive = driverRunning
        }).ToList();
    }

    private static int LayoutRegionCount(LayoutType layoutType) => layoutType switch
    {
        LayoutType.TwoVertical or LayoutType.TwoHorizontal => 2,
        LayoutType.ThreeVertical => 3,
        LayoutType.FourGrid => 4,
        _ => 2
    };

    public static int? ParseMonitorCount(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var count))
        {
            return count;
        }

        return null;
    }
}

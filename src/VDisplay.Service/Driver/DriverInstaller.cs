using System.Runtime.InteropServices;
using VDisplay.Core;

namespace VDisplay.Service.Driver;

public sealed class DriverInstaller : IDisposable
{
    private readonly object _sync = new();

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                try
                {
                    return VDisplayNative.VDisplayIsSoftwareDeviceRunning();
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
            }
        }
    }

    public Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                try
                {
                    if (!IsRunning)
                    {
                        var hr = VDisplayNative.VDisplayStartSoftwareDevice();
                        if (hr < 0)
                        {
                            throw new InvalidOperationException(
                                $"Surucu baslatilamadi: 0x{hr:X8}. Servisi yonetici olarak calistirin ve install-driver.ps1 calistirin.");
                        }
                    }

                    // SwDeviceCreate alone can leave CM_PROB_REINSTALL (0xC0000494) —
                    // bind the published INF to the software device.
                    TryBindFunctionDriver();
                    return true;
                }
                catch (DllNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "VDisplayNative.dll bulunamadi. Once scripts\\build-native.ps1 calistirin.", ex);
                }
            }
        }, cancellationToken);
    }

    public void Stop()
    {
        lock (_sync)
        {
            try
            {
                VDisplayNative.VDisplayStopSoftwareDevice();
            }
            catch (DllNotFoundException)
            {
            }
        }
    }

    public void Dispose() => Stop();

    private static void TryBindFunctionDriver()
    {
        var inf = FindDriverInf();
        if (inf is null)
        {
            return;
        }

        string[] hardwareIds =
        [
            "VDisplayDriver",
            @"Root\VDisplayDriver",
            @"SWD\VDisplayDriver\VDisplayDriver"
        ];

        foreach (var hwId in hardwareIds)
        {
            if (UpdateDriverForPlugAndPlayDevices(
                    IntPtr.Zero,
                    hwId,
                    inf,
                    InstallFlagForce,
                    out _))
            {
                return;
            }
        }
    }

    private static string? FindDriverInf()
    {
        var candidates = new List<string>();

        // Service bin -> repo dist\driver
        var baseDir = AppContext.BaseDirectory;
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "dist", "driver", "VDisplayDriver.inf")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "dist", "driver", "VDisplayDriver.inf")));

        var root = Environment.GetEnvironmentVariable("VDISPLAY_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
        {
            candidates.Add(Path.Combine(root, "dist", "driver", "VDisplayDriver.inf"));
        }

        // Published DriverStore packages
        try
        {
            var store = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "DriverStore", "FileRepository");
            if (Directory.Exists(store))
            {
                foreach (var dir in Directory.EnumerateDirectories(store, "vdisplaydriver.inf_*"))
                {
                    candidates.Add(Path.Combine(dir, "VDisplayDriver.inf"));
                }
            }
        }
        catch
        {
            // ignore
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private const uint InstallFlagForce = 0x1;

    [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UpdateDriverForPlugAndPlayDevices(
        IntPtr hwndParent,
        string hardwareId,
        string fullInfPath,
        uint installFlags,
        out bool rebootRequired);
}

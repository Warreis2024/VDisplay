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
                if (IsRunning)
                {
                    return true;
                }

                try
                {
                    var hr = VDisplayNative.VDisplayStartSoftwareDevice();
                    if (hr < 0)
                    {
                        throw new InvalidOperationException($"Surucu baslatilamadi: 0x{hr:X8}. Servisi yonetici olarak calistirin ve install-driver.ps1 calistirin.");
                    }

                    return true;
                }
                catch (DllNotFoundException ex)
                {
                    throw new InvalidOperationException("VDisplayNative.dll bulunamadi. Once scripts\\build-native.ps1 calistirin.", ex);
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
}

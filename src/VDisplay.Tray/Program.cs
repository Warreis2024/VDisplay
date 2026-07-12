using System.Runtime.Versioning;

namespace VDisplay.Tray;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

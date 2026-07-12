using System.Text;
using System.Text.Json;
using VDisplay.Core;
using VDisplay.Core.Models;

namespace VDisplay.Cli;

internal static class Program
{
    private static readonly NamedPipeIpcClient Client = new();

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        if (args.Length == 0)
        {
            return await RunMenuAsync();
        }

        return await RunCommandAsync(args);
    }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        try
        {
            var command = args[0].ToLowerInvariant();
            return command switch
            {
                "help" or "-h" or "--help" => PrintHelp(),
                "ping" => await PingAsync(),
                "status" => await ShowStatusAsync(),
                "driver" => await DriverAsync(args),
                "vm" => await VmAsync(args),
                "layout" => await LayoutAsync(args),
                "monitors" => await MonitorsAsync(args),
                "source" => await SourceAsync(args),
                "capture" => await CaptureAsync(args),
                "vm-split" => await VmSplitAsync(args),
                "physical-split" => await VmSplitAsync(args),
                _ => Unknown(args[0])
            };
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Hata: {ex.Message}");
            Console.Error.WriteLine("Servis calisiyor mu? dotnet run --project src\\VDisplay.Service");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hata: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunMenuAsync()
    {
        Console.WriteLine("VDisplay CLI - Servis menusu");
        Console.WriteLine("Servis: dotnet run --project src\\VDisplay.Service");
        Console.WriteLine();

        while (true)
        {
            PrintMenu();
            Console.Write("Secim: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                continue;
            }

            if (input is "0" or "q" or "exit")
            {
                return 0;
            }

            try
            {
                var ok = input switch
                {
                    "1" => await PingAsync() == 0,
                    "2" => await ShowStatusAsync() == 0,
                    "3" => await SendSimpleAsync(IpcCommand.StartDriver, "Surucu baslat") == 0,
                    "4" => await SendSimpleAsync(IpcCommand.StopDriver, "Surucu durdur") == 0,
                    "5" => await VmSetAsync(2),
                    "6" => await VmSetAsync(3),
                    "7" => await VmSetAsync(4),
                    "8" => await LayoutSetAsync(LayoutType.TwoVertical),
                    "9" => await LayoutSetAsync(LayoutType.TwoHorizontal),
                    "10" => await LayoutSetAsync(LayoutType.ThreeVertical),
                    "11" => await LayoutSetAsync(LayoutType.FourGrid),
                    "12" => await SendSimpleAsync(IpcCommand.StartCapture, "Capture baslat") == 0,
                    "13" => await SendSimpleAsync(IpcCommand.StopCapture, "Capture durdur") == 0,
                    "14" => await ShowPhysicalMonitorsAsync() == 0,
                    "15" => await VmSplitSetupAsync("dual") == 0,
                    "16" => await SendSimpleAsync(IpcCommand.StopVmSplit, "VM split durdur") == 0,
                    _ => false
                };

                if (!ok && input is not ("1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or "10" or "11" or "12" or "13" or "14" or "15" or "16"))
                {
                    Console.WriteLine("Gecersiz secim.");
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Baglanti hatasi: {ex.Message}");
                Console.WriteLine("Once servisi baslat.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static void PrintMenu()
    {
        Console.WriteLine("--- Komutlar ---");
        Console.WriteLine(" 1) Ping");
        Console.WriteLine(" 2) Durum");
        Console.WriteLine(" 3) Surucu baslat");
        Console.WriteLine(" 4) Surucu durdur");
        Console.WriteLine(" 5) VM sayisi = 2");
        Console.WriteLine(" 6) VM sayisi = 3");
        Console.WriteLine(" 7) VM sayisi = 4");
        Console.WriteLine(" 8) Duzen: 2 Dikey");
        Console.WriteLine(" 9) Duzen: 2 Yatay");
        Console.WriteLine("10) Duzen: 3 Dikey");
        Console.WriteLine("11) Duzen: 4 Grid");
        Console.WriteLine("12) Capture baslat");
        Console.WriteLine("13) Capture durdur");
        Console.WriteLine("14) Fiziksel monitörler");
        Console.WriteLine("15) VM split baslat (dual)");
        Console.WriteLine("16) VM split durdur");
        Console.WriteLine(" 0) Cikis");
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            VDisplay CLI

            Kullanim:
              dotnet run --project src\VDisplay.Cli -- [komut]

            Komutlar:
              ping
              status
              driver start|stop
              vm set <2|3|4>
              layout set <TwoVertical|TwoHorizontal|ThreeVertical|FourGrid>
              monitors physical
              source set <index>
              capture start|stop
              vm-split setup [primary|dual]|stop
              physical-split start [primary|dual]|stop  (vm-split ile ayni)

            VM split: fiziksel ekrani VM'lere bol; Windows ayri monitör görür.
            Meeting: pencereyi VM'ye surukle -> o VM'yi Meet'te paylas

            Ornek (2 fiziksel x 2 yari = 4 VM):
              dotnet run --project src\VDisplay.Cli -- driver start
              dotnet run --project src\VDisplay.Cli -- vm-split setup dual
              dotnet run --project src\VDisplay.Cli -- monitors all

            Etkilesimli menu (argumansiz):
              dotnet run --project src\VDisplay.Cli
            """);
        return 0;
    }

    private static int Unknown(string arg)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: {arg}");
        PrintHelp();
        return 1;
    }

    private static async Task<int> PingAsync()
    {
        var response = await Client.SendAsync(new IpcRequest { Command = IpcCommand.Ping });
        PrintResponse(response);
        return response.Success ? 0 : 1;
    }

    private static async Task<int> ShowStatusAsync()
    {
        var response = await Client.SendAsync(new IpcRequest { Command = IpcCommand.GetStatus });
        if (!response.Success)
        {
            PrintResponse(response);
            return 1;
        }

        if (string.IsNullOrWhiteSpace(response.Data))
        {
            Console.WriteLine(response.Message ?? "Durum alinamadi.");
            return 1;
        }

        var status = JsonSerializer.Deserialize<ServiceStatus>(response.Data);
        if (status is null)
        {
            Console.WriteLine("Durum parse edilemedi.");
            return 1;
        }

        PrintStatus(status);
        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            Console.WriteLine($"Not: {response.Message}");
        }

        return 0;
    }

    private static async Task<int> DriverAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Kullanim: driver start|stop");
            return 1;
        }

        var action = args[1].ToLowerInvariant();
        var command = action switch
        {
            "start" => IpcCommand.StartDriver,
            "stop" => IpcCommand.StopDriver,
            _ => (IpcCommand?)null
        };

        if (command is null)
        {
            Console.Error.WriteLine("Kullanim: driver start|stop");
            return 1;
        }

        return await SendSimpleAsync(command.Value, action);
    }

    private static async Task<int> VmAsync(string[] args)
    {
        if (args.Length < 3 || !args[1].Equals("set", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(args[2], out var count) || count is < 2 or > 4)
        {
            Console.Error.WriteLine("Kullanim: vm set <2|3|4>");
            return 1;
        }

        return await VmSetAsync(count) ? 0 : 1;
    }

    private static async Task<bool> VmSetAsync(int count)
    {
        var payload = JsonSerializer.Serialize(new { count });
        var response = await Client.SendAsync(new IpcRequest
        {
            Command = IpcCommand.SetMonitorCount,
            Payload = payload
        });

        PrintResponse(response);
        if (response.Success && !string.IsNullOrWhiteSpace(response.Data))
        {
            var status = JsonSerializer.Deserialize<ServiceStatus>(response.Data);
            if (status is not null)
            {
                PrintStatus(status);
            }
        }

        return response.Success;
    }

    private static async Task<int> LayoutAsync(string[] args)
    {
        if (args.Length < 3 || !args[1].Equals("set", StringComparison.OrdinalIgnoreCase)
            || !Enum.TryParse<LayoutType>(args[2], ignoreCase: true, out var layout))
        {
            Console.Error.WriteLine("Kullanim: layout set <TwoVertical|TwoHorizontal|ThreeVertical|FourGrid>");
            return 1;
        }

        return await LayoutSetAsync(layout) ? 0 : 1;
    }

    private static async Task<bool> LayoutSetAsync(LayoutType layout)
    {
        var payload = JsonSerializer.Serialize(new { layout = layout.ToString() });
        var response = await Client.SendAsync(new IpcRequest
        {
            Command = IpcCommand.SetLayout,
            Payload = payload
        });

        PrintResponse(response);
        if (response.Success && !string.IsNullOrWhiteSpace(response.Data))
        {
            var status = JsonSerializer.Deserialize<ServiceStatus>(response.Data);
            if (status is not null)
            {
                PrintStatus(status);
            }
        }

        return response.Success;
    }

    private static async Task<int> MonitorsAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Kullanim: monitors physical|all");
            return 1;
        }

        return args[1].ToLowerInvariant() switch
        {
            "physical" => await ShowPhysicalMonitorsAsync(),
            "all" => await ShowAllMonitorsAsync(),
            _ => UnknownMonitorsUsage()
        };
    }

    private static int UnknownMonitorsUsage()
    {
        Console.Error.WriteLine("Kullanim: monitors physical|all");
        return 1;
    }

    private static async Task<int> ShowAllMonitorsAsync()
    {
        var physical = await Client.SendAsync(new IpcRequest { Command = IpcCommand.GetPhysicalMonitors });
        if (!physical.Success)
        {
            PrintResponse(physical);
            return 1;
        }

        var virtualResponse = await Client.SendAsync(new IpcRequest { Command = IpcCommand.GetVirtualMonitors });
        if (!virtualResponse.Success)
        {
            PrintResponse(virtualResponse);
            return 1;
        }

        var physicalMonitors = JsonSerializer.Deserialize<List<PhysicalMonitorInfo>>(physical.Data ?? "[]") ?? [];
        var virtualMonitors = JsonSerializer.Deserialize<List<PhysicalMonitorInfo>>(virtualResponse.Data ?? "[]") ?? [];

        Console.WriteLine("Fiziksel:");
        foreach (var monitor in physicalMonitors)
        {
            Console.WriteLine($"  [{monitor.Index}] {monitor.Name} - {monitor.Width}x{monitor.Height} @ ({monitor.X},{monitor.Y})");
        }

        Console.WriteLine("Sanal:");
        foreach (var monitor in virtualMonitors)
        {
            Console.WriteLine($"  [{monitor.Index}] {monitor.Name} - {monitor.Width}x{monitor.Height} @ ({monitor.X},{monitor.Y})");
        }

        return 0;
    }

    private static async Task<int> ShowPhysicalMonitorsAsync()
    {
        var response = await Client.SendAsync(new IpcRequest { Command = IpcCommand.GetPhysicalMonitors });
        if (!response.Success)
        {
            PrintResponse(response);
            return 1;
        }

        var monitors = JsonSerializer.Deserialize<List<PhysicalMonitorInfo>>(response.Data ?? "[]");
        if (monitors is null || monitors.Count == 0)
        {
            Console.WriteLine("Fiziksel monitor bulunamadi.");
            return 0;
        }

        foreach (var monitor in monitors)
        {
            Console.WriteLine($"[{monitor.Index}] {monitor.Name} - {monitor.Width}x{monitor.Height} @ ({monitor.X},{monitor.Y})");
        }

        return 0;
    }

    private static async Task<int> SourceAsync(string[] args)
    {
        if (args.Length < 3 || !args[1].Equals("set", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(args[2], out var index))
        {
            Console.Error.WriteLine("Kullanim: source set <index>");
            return 1;
        }

        var payload = JsonSerializer.Serialize(new { index });
        var response = await Client.SendAsync(new IpcRequest
        {
            Command = IpcCommand.SetSourceMonitor,
            Payload = payload
        });

        PrintResponse(response);
        return response.Success ? 0 : 1;
    }

    private static async Task<int> CaptureAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Kullanim: capture start|stop");
            return 1;
        }

        var command = args[1].ToLowerInvariant() switch
        {
            "start" => IpcCommand.StartCapture,
            "stop" => IpcCommand.StopCapture,
            _ => (IpcCommand?)null
        };

        if (command is null)
        {
            Console.Error.WriteLine("Kullanim: capture start|stop");
            return 1;
        }

        return await SendSimpleAsync(command.Value, args[1]);
    }

    private static async Task<int> VmSplitAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Kullanim: vm-split setup [primary|dual]|stop");
            return 1;
        }

        var action = args[1].ToLowerInvariant();
        if (action == "stop")
        {
            return await SendSimpleAsync(IpcCommand.StopVmSplit, "VM split durdur");
        }

        if (action == "setup" || action == "start")
        {
            var mode = args.Length > 2 ? args[2].ToLowerInvariant() : "dual";
            return await VmSplitSetupAsync(mode);
        }

        Console.Error.WriteLine("Kullanim: vm-split setup [primary|dual]|stop");
        return 1;
    }

    private static async Task<int> VmSplitSetupAsync(string mode)
    {
        var response = await Client.SendAsync(new IpcRequest
        {
            Command = IpcCommand.StartVmSplit,
            Payload = mode
        });

        PrintResponse(response);
        if (!response.Success)
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(response.Data))
        {
            var status = JsonSerializer.Deserialize<ServiceStatus>(response.Data);
            if (status is not null)
            {
                PrintStatus(status);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Sonraki adim: Ayarlar > Ekran — VM'leri fiziksel panellerin yanina hizala.");
        return 0;
    }

    private static async Task<int> SendSimpleAsync(IpcCommand command, string label)
    {
        var response = await Client.SendAsync(new IpcRequest { Command = command });
        PrintResponse(response);

        if (response.Success && !string.IsNullOrWhiteSpace(response.Data))
        {
            var status = JsonSerializer.Deserialize<ServiceStatus>(response.Data);
            if (status is not null)
            {
                PrintStatus(status);
            }
        }

        return response.Success ? 0 : 1;
    }

    private static void PrintResponse(IpcResponse response)
    {
        var state = response.Success ? "OK" : "HATA";
        Console.WriteLine($"[{state}] {response.Message ?? "-"}");
    }

    private static void PrintStatus(ServiceStatus status)
    {
        Console.WriteLine($"Surucu      : {(status.DriverRunning ? "ACIK" : "KAPALI")}");
        Console.WriteLine($"Capture     : {(status.CaptureRunning ? "ACIK" : "KAPALI")}");
        Console.WriteLine($"Phys. split : {(status.PhysicalSplitRunning ? "ACIK" : "KAPALI")}");
        Console.WriteLine($"VM sayisi   : {status.MonitorCount}");
        Console.WriteLine($"Duzen       : {status.CurrentLayout}");
        Console.WriteLine($"Kaynak mon. : {status.SourceMonitorIndex}");

        if (status.Monitors.Count == 0)
        {
            Console.WriteLine("Sanal monitor: yok");
            return;
        }

        Console.WriteLine("Sanal monitorler:");
        foreach (var monitor in status.Monitors)
        {
            var active = monitor.IsActive ? " *" : "";
            Console.WriteLine($"  - {monitor.Name} {monitor.Width}x{monitor.Height}{active}");
        }
    }
}

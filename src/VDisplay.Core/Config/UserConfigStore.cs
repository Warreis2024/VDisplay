using System.Text.Json;
using System.Text.Json.Serialization;

namespace VDisplay.Core.Config;

public sealed class DisplayModeConfig
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; } = 60;
}

public sealed class VDisplayUserConfig
{
    public int Version { get; set; } = 1;

    /// <summary>Aktif sanal monitör sayısı (1–10).</summary>
    public int MonitorCount { get; set; } = 4;

    /// <summary>
    /// dual = 2 ekran→4 VM split+capture,
    /// primary = 1 ekran→2 VM split+capture,
    /// desktop = sadece ek masaüstü (capture yok)
    /// </summary>
    public string SplitMode { get; set; } = "dual";

    /// <summary>Tercih edilen mod indeksi (Modes listesinde).</summary>
    public int PreferredModeIndex { get; set; }

    /// <summary>Windows Ekran ayarlarında görünecek çözünürlükler.</summary>
    public List<DisplayModeConfig> Modes { get; set; } =
    [
        new() { Width = 1280, Height = 1080, RefreshRate = 60 },
        new() { Width = 1920, Height = 1080, RefreshRate = 60 },
        new() { Width = 1600, Height = 900, RefreshRate = 60 },
        new() { Width = 1024, Height = 768, RefreshRate = 60 },
        new() { Width = 720, Height = 720, RefreshRate = 60 }
    ];
}

public static class UserConfigStore
{
    public const int MaxModes = 16;
    public const string ModesFileName = "modes.cfg";
    public const string JsonFileName = "vdisplay.user.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ProgramDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VDisplay");

    public static string JsonPath => Path.Combine(ProgramDataDir, JsonFileName);
    public static string ModesCfgPath => Path.Combine(ProgramDataDir, ModesFileName);

    public static VDisplayUserConfig LoadOrCreate()
    {
        Directory.CreateDirectory(ProgramDataDir);

        if (File.Exists(JsonPath))
        {
            try
            {
                var json = File.ReadAllText(JsonPath);
                var cfg = JsonSerializer.Deserialize<VDisplayUserConfig>(json, JsonOptions);
                if (cfg is not null)
                {
                    Normalize(cfg);
                    return cfg;
                }
            }
            catch
            {
                // fall through to default
            }
        }

        var defaults = new VDisplayUserConfig();
        Save(defaults);
        return defaults;
    }

    public static void Save(VDisplayUserConfig config)
    {
        Normalize(config);
        Directory.CreateDirectory(ProgramDataDir);
        File.WriteAllText(JsonPath, JsonSerializer.Serialize(config, JsonOptions));
        WriteModesCfg(config);
    }

    /// <summary>
    /// Sürücünün okuduğu düz metin dosya.
    /// Biçim: VDISPLAY_MODES 1 / count preferred / w h hz ...
    /// </summary>
    public static void WriteModesCfg(VDisplayUserConfig config)
    {
        Normalize(config);
        Directory.CreateDirectory(ProgramDataDir);

        using var writer = new StreamWriter(ModesCfgPath, false);
        writer.WriteLine("VDISPLAY_MODES 1");
        writer.WriteLine($"{config.Modes.Count} {config.PreferredModeIndex}");
        foreach (var mode in config.Modes)
        {
            writer.WriteLine($"{mode.Width} {mode.Height} {mode.RefreshRate}");
        }
    }

    private static void Normalize(VDisplayUserConfig config)
    {
        config.MonitorCount = Math.Clamp(config.MonitorCount, 1, 10);
        config.SplitMode = config.SplitMode?.Trim().ToLowerInvariant() switch
        {
            "primary" or "single" => "primary",
            "desktop" or "extend" or "empty" or "workspace" => "desktop",
            _ => "dual"
        };

        if (config.Modes is null || config.Modes.Count == 0)
        {
            config.Modes =
            [
                new() { Width = 1280, Height = 1080, RefreshRate = 60 },
                new() { Width = 720, Height = 720, RefreshRate = 60 }
            ];
        }

        config.Modes = config.Modes
            .Where(m => m.Width >= 640 && m.Height >= 480 && m.Width <= 3840 && m.Height <= 2160)
            .Select(m => new DisplayModeConfig
            {
                Width = m.Width,
                Height = m.Height,
                RefreshRate = m.RefreshRate is > 0 and <= 240 ? m.RefreshRate : 60
            })
            .Take(MaxModes)
            .ToList();

        if (config.Modes.Count == 0)
        {
            config.Modes.Add(new DisplayModeConfig { Width = 1280, Height = 1080, RefreshRate = 60 });
        }

        config.PreferredModeIndex = Math.Clamp(config.PreferredModeIndex, 0, config.Modes.Count - 1);
    }
}

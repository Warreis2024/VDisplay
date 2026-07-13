using System.Globalization;
using System.Text.Json;

namespace VDisplay.Helper;

internal static class Localization
{
    private static Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    public static string CurrentLanguage { get; private set; } = "tr";

    public static void Load(string language)
    {
        var code = Normalize(language);
        var path = FindLangFile(code);
        if (path is null && code != "en")
        {
            path = FindLangFile("en");
            code = "en";
        }

        if (path is null)
        {
            _strings = EmbeddedFallback(code);
            CurrentLanguage = code;
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            _strings = dict ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CurrentLanguage = code;
        }
        catch
        {
            _strings = EmbeddedFallback(code);
            CurrentLanguage = code;
        }
    }

    public static string T(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;

    public static string T(string key, params object[] args)
    {
        var format = T(key);
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase)
                ? "tr"
                : "en";
        }

        var code = language.Trim().ToLowerInvariant();
        return code is "tr" or "tr-tr" ? "tr" : "en";
    }

    private static string? FindLangFile(string code)
    {
        foreach (var dir in CandidateDirs())
        {
            var path = Path.Combine(dir, $"{code}.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirs()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "lang");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, "lang");
            yield return Path.Combine(dir.FullName, "src", "VDisplay.Helper", "lang");
            if (File.Exists(Path.Combine(dir.FullName, "VDisplay.sln")))
            {
                yield break;
            }

            dir = dir.Parent;
        }
    }

    private static Dictionary<string, string> EmbeddedFallback(string code) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["app_title"] = code == "en" ? "VDisplay Helper" : "VDisplay Yardımcı",
            ["btn_0"] = code == "en" ? "0. First setup" : "0. İlk kurulum",
            ["btn_1"] = code == "en" ? "1. Start" : "1. Başlat"
        };
}

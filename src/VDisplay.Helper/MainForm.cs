using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using VDisplay.Core.Config;

namespace VDisplay.Helper;

[SupportedOSPlatform("windows")]
internal sealed class MainForm : Form
{
    private readonly TextBox _log;
    private readonly NumericUpDown _monitorCount;
    private readonly ComboBox _splitMode;
    private readonly ListBox _modesList;
    private readonly NumericUpDown _modeW;
    private readonly NumericUpDown _modeH;
    private readonly NumericUpDown _modeHz;
    private VDisplayUserConfig _config = UserConfigStore.LoadOrCreate();
    private Process? _serviceProcess;

    public MainForm()
    {
        Text = "VDisplay Yardımcı";
        Width = 720;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(640, 520);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        // --- Büyük aksiyonlar ---
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        actions.Controls.Add(BigButton("1. Başlat", Color.FromArgb(46, 125, 50), async (_, _) => await StartAllAsync()));
        actions.Controls.Add(BigButton("Durdur", Color.FromArgb(198, 40, 40), (_, _) => StopAll()));
        actions.Controls.Add(BigButton("Tray önizleme", Color.FromArgb(25, 118, 210), (_, _) => StartTray()));
        actions.Controls.Add(BigButton("Ekran ayarları", Color.FromArgb(69, 90, 100), (_, _) => OpenDisplaySettings()));
        root.Controls.Add(actions, 0, 0);

        var setup = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        setup.Controls.Add(BigButton("İlk kurulum (sürücü)", Color.FromArgb(94, 53, 177), async (_, _) => await FirstTimeSetupAsync()));
        setup.Controls.Add(BigButton("Ayarları kaydet", Color.FromArgb(0, 105, 92), (_, _) => SaveSettings()));
        setup.Controls.Add(BigButton("JSON klasörü", Color.Gray, (_, _) => OpenConfigFolder()));
        root.Controls.Add(setup, 0, 1);

        // --- Ayar paneli ---
        var settings = new GroupBox { Text = "Kullanıcı ayarları (JSON)", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var settingsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        left.Controls.Add(new Label { Text = "VM sayısı (2–4)", AutoSize = true });
        _monitorCount = new NumericUpDown { Minimum = 2, Maximum = 4, Value = _config.MonitorCount, Width = 80 };
        left.Controls.Add(_monitorCount);
        left.Controls.Add(new Label { Text = "Kullanım modu", AutoSize = true, Margin = new Padding(0, 12, 0, 0) });
        _splitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        _splitMode.Items.AddRange(
        [
            "desktop — sadece ek masaüstü (capture yok)",
            "dual — 2 ekran → 4 VM (split)",
            "primary — 1 ekran → 2 VM (split)"
        ]);
        _splitMode.SelectedIndex = _config.SplitMode switch
        {
            "primary" => 2,
            "dual" => 1,
            _ => 0
        };
        left.Controls.Add(_splitMode);
        left.Controls.Add(new Label
        {
            Text = "Çözünürlük ekle:",
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0)
        });
        var addRow = new FlowLayoutPanel { AutoSize = true };
        _modeW = new NumericUpDown { Minimum = 640, Maximum = 3840, Value = 720, Width = 70 };
        _modeH = new NumericUpDown { Minimum = 480, Maximum = 2160, Value = 720, Width = 70 };
        _modeHz = new NumericUpDown { Minimum = 30, Maximum = 240, Value = 60, Width = 55 };
        addRow.Controls.Add(_modeW);
        addRow.Controls.Add(new Label { Text = "x", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        addRow.Controls.Add(_modeH);
        addRow.Controls.Add(new Label { Text = "@", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        addRow.Controls.Add(_modeHz);
        left.Controls.Add(addRow);
        var addBtn = new Button { Text = "Listeye ekle", AutoSize = true };
        addBtn.Click += (_, _) => AddMode();
        left.Controls.Add(addBtn);
        var removeBtn = new Button { Text = "Seçileni sil", AutoSize = true };
        removeBtn.Click += (_, _) => RemoveSelectedMode();
        left.Controls.Add(removeBtn);

        _modesList = new ListBox { Dock = DockStyle.Fill };
        RefreshModesList();
        settingsLayout.Controls.Add(left, 0, 0);
        settingsLayout.SetRowSpan(left, 3);
        settingsLayout.Controls.Add(_modesList, 1, 0);
        settingsLayout.SetRowSpan(_modesList, 3);
        settings.Controls.Add(settingsLayout);
        root.Controls.Add(settings, 0, 2);

        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };
        var logBox = new GroupBox { Text = "Durum", Dock = DockStyle.Fill };
        logBox.Controls.Add(_log);
        root.Controls.Add(logBox, 0, 3);

        Controls.Add(root);
        Log($"Ayar dosyası: {UserConfigStore.JsonPath}");
        Log("desktop = ek masaüstü | dual/primary = fiziksel ekranı VM'lere böl.");
        Log("Günlük: mod seç → «1. Başlat» → Ekran ayarları.");
    }

    private static Button BigButton(string text, Color back, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 42,
            Width = 160,
            Margin = new Padding(0, 0, 8, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    private void RefreshModesList()
    {
        _modesList.Items.Clear();
        for (var i = 0; i < _config.Modes.Count; i++)
        {
            var m = _config.Modes[i];
            var star = i == _config.PreferredModeIndex ? " ★" : "";
            _modesList.Items.Add($"{m.Width}×{m.Height} @{m.RefreshRate}Hz{star}");
        }
    }

    private void PullUiToConfig()
    {
        _config.MonitorCount = (int)_monitorCount.Value;
        _config.SplitMode = _splitMode.SelectedIndex switch
        {
            1 => "dual",
            2 => "primary",
            _ => "desktop"
        };
    }

    private void AddMode()
    {
        PullUiToConfig();
        _config.Modes.Add(new DisplayModeConfig
        {
            Width = (int)_modeW.Value,
            Height = (int)_modeH.Value,
            RefreshRate = (int)_modeHz.Value
        });
        RefreshModesList();
        Log($"Eklendi: {_modeW.Value}x{_modeH.Value} — «Ayarları kaydet» de.");
    }

    private void RemoveSelectedMode()
    {
        if (_modesList.SelectedIndex < 0 || _config.Modes.Count <= 1)
        {
            Log("En az bir çözünürlük kalmalı.");
            return;
        }

        _config.Modes.RemoveAt(_modesList.SelectedIndex);
        if (_config.PreferredModeIndex >= _config.Modes.Count)
        {
            _config.PreferredModeIndex = 0;
        }

        RefreshModesList();
    }

    private void SaveSettings()
    {
        PullUiToConfig();
        if (_modesList.SelectedIndex >= 0)
        {
            _config.PreferredModeIndex = _modesList.SelectedIndex;
        }

        UserConfigStore.Save(_config);
        Log($"Kaydedildi: {UserConfigStore.JsonPath}");
        Log($"Sürücü dosyası: {UserConfigStore.ModesCfgPath}");
        Log("Çözünürlüklerin Windows'ta görünmesi için sürücüyü yeniden başlat (Durdur → Başlat) veya İlk kurulum.");
        RefreshModesList();
    }

    private void OpenConfigFolder()
    {
        Directory.CreateDirectory(UserConfigStore.ProgramDataDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = UserConfigStore.ProgramDataDir,
            UseShellExecute = true
        });
    }

    private static void OpenDisplaySettings() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "ms-settings:display",
            UseShellExecute = true
        });

    private async Task FirstTimeSetupAsync()
    {
        Log("İlk kurulum: test imzalama + sürücü (yönetici gerekir)...");
        PullUiToConfig();
        UserConfigStore.Save(_config);

        var root = FindRepoRoot();
        if (root is null)
        {
            Log("HATA: Proje kökü bulunamadı.");
            return;
        }

        await RunElevatedScriptAsync(Path.Combine(root, "scripts", "enable-test-signing.ps1"));
        Log("Not: Test imzalama yeni açıldıysa bilgisayarı yeniden başlat, sonra tekrar «İlk kurulum».");
        await RunElevatedScriptAsync(Path.Combine(root, "scripts", "install-driver.ps1"));
        Log("Sürücü kurulumu denendi.");
    }

    private async Task StartAllAsync()
    {
        PullUiToConfig();
        UserConfigStore.Save(_config);

        var root = FindRepoRoot();
        if (root is null)
        {
            Log("HATA: Proje kökü bulunamadı (VDisplay.sln yanında çalıştır).");
            return;
        }

        EnsureService(root);
        await Task.Delay(1500);

        var code = await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["driver", "start"]);
        if (code != 0)
        {
            Log("Sürücü start başarısız — önce «İlk kurulum» yap.");
            return;
        }

        await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm", "set", _config.MonitorCount.ToString()]);

        if (_config.SplitMode == "desktop")
        {
            // Önceki split/capture varsa kapat; VM'ler boş ek masaüstü kalsın.
            await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm-split", "stop"]);
            Log("Masaüstü modu: VM'ler sadece ek ekran (capture yok).");
            Log("«Ekran ayarları» → Genişlet → pencereleri VM'lere taşı.");
            return;
        }

        await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm-split", "setup", _config.SplitMode]);
        Log("Split modu aktif. «Ekran ayarları» → VM'leri yan yana yerleştir.");
        Log("İstersen «Tray önizleme» ile canlı bak.");
    }

    private void StopAll()
    {
        var root = FindRepoRoot();
        if (root is not null)
        {
            _ = RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm-split", "stop"]);
            _ = RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["driver", "stop"]);
        }

        if (_serviceProcess is { HasExited: false })
        {
            try { _serviceProcess.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        foreach (var p in Process.GetProcessesByName("VDisplay.Service"))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        foreach (var p in Process.GetProcessesByName("VDisplay.Tray"))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        Log("Durduruldu.");
    }

    private void StartTray()
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project \"src\\VDisplay.Tray\\VDisplay.Tray.csproj\" -c Release",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Log("Tray başlatıldı.");
    }

    private void EnsureService(string root)
    {
        if (Process.GetProcessesByName("VDisplay.Service").Length > 0)
        {
            Log("Servis zaten çalışıyor.");
            return;
        }

        _serviceProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project \"src\\VDisplay.Service\\VDisplay.Service.csproj\" -c Release",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Log("Servis başlatıldı.");
    }

    private async Task<int> RunDotnetAsync(string root, string project, string[] args)
    {
        var argLine = $"run --project \"{project}\" -c Release -- {string.Join(' ', args)}";
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = argLine,
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";

        using var p = Process.Start(psi);
        if (p is null)
        {
            return 1;
        }

        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Log(stdout.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stderr) && p.ExitCode != 0)
        {
            Log(stderr.Trim());
        }

        return p.ExitCode;
    }

    private async Task RunElevatedScriptAsync(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            Log($"Script yok: {scriptPath}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is not null)
            {
                await p.WaitForExitAsync();
                Log($"Script bitti ({Path.GetFileName(scriptPath)}), kod={p.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            Log($"Yönetici onayı iptal / hata: {ex.Message}");
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VDisplay.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        var cwd = new DirectoryInfo(Environment.CurrentDirectory);
        while (cwd is not null)
        {
            if (File.Exists(Path.Combine(cwd.FullName, "VDisplay.sln")))
            {
                return cwd.FullName;
            }

            cwd = cwd.Parent;
        }

        return null;
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_log.TextLength > 0)
        {
            _log.AppendText(Environment.NewLine);
        }

        _log.AppendText(line);
    }
}

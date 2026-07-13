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
    private readonly ComboBox _language;
    private readonly ListBox _modesList;
    private readonly NumericUpDown _modeW;
    private readonly NumericUpDown _modeH;
    private readonly NumericUpDown _modeHz;
    private readonly Button[] _actionButtons = new Button[8];
    private readonly GroupBox _settingsGroup;
    private readonly GroupBox _logBox;
    private readonly Label _labelLanguage;
    private readonly Label _labelVmCount;
    private readonly Label _labelMode;
    private readonly Label _labelAddRes;
    private readonly Button _addBtn;
    private readonly Button _removeBtn;
    private VDisplayUserConfig _config = UserConfigStore.LoadOrCreate();
    private Process? _serviceProcess;
    private bool _suppressLanguageEvent;

    public MainForm()
    {
        Localization.Load(_config.Language);

        Width = 720;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(640, 540);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        for (var c = 0; c < 4; c++)
        {
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        }

        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        void Place(int col, int row, int index, Color color, EventHandler onClick)
        {
            var btn = BigButton("", color, onClick);
            btn.Dock = DockStyle.Fill;
            btn.Margin = new Padding(4);
            _actionButtons[index] = btn;
            actions.Controls.Add(btn, col, row);
        }

        Place(0, 0, 0, Color.FromArgb(94, 53, 177), async (_, _) => await FirstTimeSetupAsync());
        Place(1, 0, 1, Color.FromArgb(46, 125, 50), async (_, _) => await StartAllAsync());
        Place(2, 0, 2, Color.FromArgb(25, 118, 210), (_, _) => StartTray());
        Place(3, 0, 3, Color.FromArgb(13, 71, 161), (_, _) => StopTray());
        Place(0, 1, 4, Color.FromArgb(198, 40, 40), (_, _) => StopAll());
        Place(1, 1, 5, Color.FromArgb(69, 90, 100), (_, _) => OpenDisplaySettings());
        Place(2, 1, 6, Color.FromArgb(0, 105, 92), (_, _) => SaveSettings());
        Place(3, 1, 7, Color.Gray, (_, _) => OpenConfigFolder());
        root.Controls.Add(actions, 0, 0);

        _settingsGroup = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var settingsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        _labelLanguage = new Label { AutoSize = true };
        left.Controls.Add(_labelLanguage);
        _language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        _language.SelectedIndexChanged += LanguageChanged;
        left.Controls.Add(_language);

        _labelVmCount = new Label { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        left.Controls.Add(_labelVmCount);
        _monitorCount = new NumericUpDown { Minimum = 1, Maximum = 10, Value = Math.Clamp(_config.MonitorCount, 1, 10), Width = 80 };
        left.Controls.Add(_monitorCount);

        _labelMode = new Label { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        left.Controls.Add(_labelMode);
        _splitMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        left.Controls.Add(_splitMode);

        _labelAddRes = new Label { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
        left.Controls.Add(_labelAddRes);
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

        _addBtn = new Button { AutoSize = true };
        _addBtn.Click += (_, _) => AddMode();
        left.Controls.Add(_addBtn);
        _removeBtn = new Button { AutoSize = true };
        _removeBtn.Click += (_, _) => RemoveSelectedMode();
        left.Controls.Add(_removeBtn);

        _modesList = new ListBox { Dock = DockStyle.Fill };
        settingsLayout.Controls.Add(left, 0, 0);
        settingsLayout.SetRowSpan(left, 3);
        settingsLayout.Controls.Add(_modesList, 1, 0);
        settingsLayout.SetRowSpan(_modesList, 3);
        _settingsGroup.Controls.Add(settingsLayout);
        root.Controls.Add(_settingsGroup, 0, 1);

        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };
        _logBox = new GroupBox { Dock = DockStyle.Fill };
        _logBox.Controls.Add(_log);
        root.Controls.Add(_logBox, 0, 2);

        Controls.Add(root);
        ApplyUiLanguage(logIntro: true);
    }

    private void LanguageChanged(object? sender, EventArgs e)
    {
        if (_suppressLanguageEvent || _language.SelectedIndex < 0)
        {
            return;
        }

        var code = _language.SelectedIndex == 0 ? "tr" : "en";
        if (code == Localization.CurrentLanguage)
        {
            return;
        }

        _config.Language = code;
        Localization.Load(code);
        UserConfigStore.Save(_config);
        ApplyUiLanguage(logIntro: false);
        LogT("log_language_changed", Localization.T(code == "tr" ? "lang_tr" : "lang_en"));
    }

    private void ApplyUiLanguage(bool logIntro)
    {
        Text = Localization.T("app_title");
        for (var i = 0; i < _actionButtons.Length; i++)
        {
            _actionButtons[i].Text = Localization.T($"btn_{i}");
        }

        _settingsGroup.Text = Localization.T("group_settings");
        _logBox.Text = Localization.T("group_status");
        _labelLanguage.Text = Localization.T("label_language");
        _labelVmCount.Text = Localization.T("label_vm_count");
        _labelMode.Text = Localization.T("label_mode");
        _labelAddRes.Text = Localization.T("label_add_res");
        _addBtn.Text = Localization.T("btn_add_mode");
        _removeBtn.Text = Localization.T("btn_remove_mode");

        _suppressLanguageEvent = true;
        var langIndex = Localization.CurrentLanguage == "en" ? 1 : 0;
        _language.Items.Clear();
        _language.Items.Add(Localization.T("lang_tr"));
        _language.Items.Add(Localization.T("lang_en"));
        _language.SelectedIndex = langIndex;
        _suppressLanguageEvent = false;

        var splitIndex = _config.SplitMode switch
        {
            "primary" => 2,
            "dual" => 1,
            _ => 0
        };
        _splitMode.Items.Clear();
        _splitMode.Items.Add(Localization.T("mode_desktop"));
        _splitMode.Items.Add(Localization.T("mode_dual"));
        _splitMode.Items.Add(Localization.T("mode_primary"));
        _splitMode.SelectedIndex = splitIndex;

        RefreshModesList();

        if (logIntro)
        {
            LogT("log_config_path", UserConfigStore.JsonPath);
            LogT("log_flow");
        }
    }

    private static Button BigButton(string text, Color back, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            UseVisualStyleBackColor = false
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
        _config.Language = Localization.CurrentLanguage;
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
        LogT("log_mode_added", _modeW.Value, _modeH.Value);
    }

    private void RemoveSelectedMode()
    {
        if (_modesList.SelectedIndex < 0 || _config.Modes.Count <= 1)
        {
            LogT("log_keep_one_mode");
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
        LogT("log_saved", UserConfigStore.JsonPath);
        LogT("log_modes_cfg", UserConfigStore.ModesCfgPath);
        LogT("log_restart_for_modes");
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
        LogT("log_setup_start");
        PullUiToConfig();
        UserConfigStore.Save(_config);

        var root = FindRepoRoot();
        if (root is null)
        {
            LogT("log_no_repo");
            return;
        }

        var signCode = await RunElevatedScriptAsync(Path.Combine(root, "scripts", "enable-test-signing.ps1"));
        AppendProgramDataLog("enable-test-signing.log");

        // 2 = yeni açıldı → reboot şart; kurulum yapma
        if (signCode == 2)
        {
            LogT("log_reboot_required_now");
            LogT("log_reboot_again");
            return;
        }

        if (signCode != 0)
        {
            LogT("log_testsign_fail");
            return;
        }

        if (!IsTestSigningActive())
        {
            LogT("log_reboot_required_now");
            LogT("log_reboot_again");
            return;
        }

        LogT("log_testsign_ok");

        var packageDir = Path.Combine(root, "dist", "driver");
        var inf = Path.Combine(packageDir, "VDisplayDriver.inf");
        var dll = Path.Combine(packageDir, "VDisplayDriver.dll");
        var cat = Directory.Exists(packageDir)
            ? Directory.GetFiles(packageDir, "*.cat").FirstOrDefault()
            : null;
        if (!File.Exists(inf) || !File.Exists(dll) || cat is null)
        {
            LogT("log_no_package");
            LogT("log_expected", packageDir);
            LogT("log_end_user_dist");
            return;
        }

        LogT("log_package", packageDir);

        var installCode = await RunElevatedScriptAsync(Path.Combine(root, "scripts", "install-driver.ps1"));
        AppendProgramDataLog("install-driver.log");
        if (installCode != 0)
        {
            LogT("log_install_fail", installCode);
            LogT("log_hint_reboot");
            LogT("log_hint_admin");
            LogT("log_hint_manual");
            return;
        }

        EnsureNativeDll(root);
        LogT("log_install_ok");
    }

    private async Task StartAllAsync()
    {
        PullUiToConfig();
        UserConfigStore.Save(_config);

        var root = FindRepoRoot();
        if (root is null)
        {
            LogT("log_no_repo");
            return;
        }

        if (!EnsureNativeDll(root))
        {
            LogT("log_native_missing");
            return;
        }

        EnsureService(root);
        LogT("log_waiting_service");
        if (!await WaitForServiceAsync(root, TimeSpan.FromSeconds(90)))
        {
            LogT("log_service_fail");
            LogT("log_service_manual");
            return;
        }

        var code = await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["driver", "start"]);
        if (code != 0)
        {
            LogT("log_driver_start_fail");
            return;
        }

        await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm", "set", _config.MonitorCount.ToString()]);

        if (_config.SplitMode == "desktop")
        {
            await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm-split", "stop"]);
            LogT("log_desktop_mode");
            LogT("log_extend_hint");
            return;
        }

        await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["vm-split", "setup", _config.SplitMode]);
        LogT("log_split_mode");
        LogT("log_tray_hint");
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

        LogT("log_stopped");
    }

    private void StopTray()
    {
        var killed = 0;
        foreach (var p in Process.GetProcessesByName("VDisplay.Tray"))
        {
            try
            {
                p.Kill(entireProcessTree: true);
                killed++;
            }
            catch
            {
                // ignore
            }
        }

        LogT(killed > 0 ? "log_tray_closed" : "log_tray_already_closed", killed);
    }

    private void StartTray()
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            return;
        }

        if (Process.GetProcessesByName("VDisplay.Tray").Length > 0)
        {
            LogT("log_tray_already_open");
            return;
        }

        LogT("log_tray_info");
        LogT("log_tray_need_start");

        Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project \"src\\VDisplay.Tray\\VDisplay.Tray.csproj\" -c Release",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        LogT("log_tray_started");
    }

    private void EnsureService(string root)
    {
        EnsureNativeDll(root);

        if (Process.GetProcessesByName("VDisplay.Service").Length > 0)
        {
            LogT("log_service_running");
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
        LogT("log_service_started");
    }

    private bool EnsureNativeDll(string root)
    {
        var src = Path.Combine(root, "dist", "native", "VDisplayNative.dll");
        if (!File.Exists(src))
        {
            return false;
        }

        var targets = new[]
        {
            Path.Combine(root, "src", "VDisplay.Service", "bin", "Release", "net8.0-windows"),
            Path.Combine(root, "src", "VDisplay.Service", "bin", "Debug", "net8.0-windows")
        };

        foreach (var dir in targets)
        {
            Directory.CreateDirectory(dir);
            File.Copy(src, Path.Combine(dir, "VDisplayNative.dll"), overwrite: true);
        }

        return true;
    }

    private static bool IsTestSigningActive()
    {
        // Çalışan çekirdek (bcdedit reboot öncesi de Yes gösterebilir)
        try
        {
            if (TryGetCodeIntegrityOptions(out var options)
                && (options & CodeIntegrityOptionTestSign) != 0)
            {
                return true;
            }
        }
        catch
        {
            // fall through
        }

        // Yedek: bcdedit (yanıltıcı olabilir)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bcdedit",
                Arguments = "/enum {current}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                return false;
            }

            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return System.Text.RegularExpressions.Regex.IsMatch(
                output,
                @"testsigning\s+Yes",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private const uint CodeIntegrityOptionTestSign = 0x00000002;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SystemCodeIntegrityInformation
    {
        public uint Length;
        public uint CodeIntegrityOptions;
    }

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int systemInformationClass,
        ref SystemCodeIntegrityInformation systemInformation,
        int systemInformationLength,
        out int returnLength);

    private static bool TryGetCodeIntegrityOptions(out uint options)
    {
        options = 0;
        var info = new SystemCodeIntegrityInformation
        {
            Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<SystemCodeIntegrityInformation>()
        };
        var status = NtQuerySystemInformation(
            0x67, // SystemCodeIntegrityInformation
            ref info,
            (int)info.Length,
            out _);
        if (status != 0)
        {
            return false;
        }

        options = info.CodeIntegrityOptions;
        return true;
    }

    private void AppendProgramDataLog(string fileName)
    {
        try
        {
            var path = Path.Combine(UserConfigStore.ProgramDataDir, fileName);
            if (!File.Exists(path))
            {
                return;
            }

            var text = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).TakeLast(12))
            {
                Log("  " + line);
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task<bool> WaitForServiceAsync(string root, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var code = await RunDotnetAsync(root, "src\\VDisplay.Cli\\VDisplay.Cli.csproj", ["ping"]);
            if (code == 0)
            {
                LogT("log_service_ready");
                return true;
            }

            await Task.Delay(1500);
        }

        return false;
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

    private async Task<int> RunElevatedScriptAsync(string scriptPath) =>
        await RunScriptAsync(scriptPath, elevate: true);

    private async Task<int> RunScriptAsync(string scriptPath, bool elevate)
    {
        if (!File.Exists(scriptPath))
        {
            LogT("log_script_missing", scriptPath);
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = GetPowerShellPath(),
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            Verb = elevate ? "runas" : null
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null)
            {
                return 1;
            }

            await p.WaitForExitAsync();
            LogT("log_script_done", Path.GetFileName(scriptPath), p.ExitCode);
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            LogT("log_script_error", ex.Message);
            return 1;
        }
    }

    private static string GetPowerShellPath()
    {
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        // 32-bit süreçte System32 WOW64'e gider; Sysnative gerçek 64-bit System32
        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            var sysnative = Path.Combine(windir, "Sysnative", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(sysnative))
            {
                return sysnative;
            }
        }

        var system32 = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(system32) ? system32 : "powershell.exe";
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

    private void LogT(string key, params object[] args) => Log(Localization.T(key, args));

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

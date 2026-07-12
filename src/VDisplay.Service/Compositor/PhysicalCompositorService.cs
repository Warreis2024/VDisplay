using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using VDisplay.Core;
using VDisplay.Core.Models;
using VDisplay.Service.Capture;

namespace VDisplay.Service.Compositor;

[SupportedOSPlatform("windows")]
public sealed class PhysicalCompositorService : IDisposable
{
    private readonly PhysicalMonitorProvider _monitorProvider;
    private readonly object _sync = new();
    private Thread? _uiThread;
    private MultiFormContext? _context;
    private volatile bool _running;
    private long _framesRendered;

    public PhysicalCompositorService(PhysicalMonitorProvider monitorProvider)
    {
        _monitorProvider = monitorProvider;
    }

    public bool IsRunning => _running;

    public long FramesRendered => Interlocked.Read(ref _framesRendered);

    public void Start(int vmCount, PhysicalSplitMode mode)
    {
        lock (_sync)
        {
            if (_running)
            {
                return;
            }

            var physicals = _monitorProvider.GetCaptureSources()
                .OrderBy(m => m.X)
                .ThenBy(m => m.Y)
                .ToList();

            if (physicals.Count == 0)
            {
                throw new InvalidOperationException("Fiziksel monitor bulunamadi.");
            }

            var panelCount = PanelCount(mode, physicals.Count, vmCount);
            physicals = physicals.Take(panelCount).ToList();

            var vms = _monitorProvider.GetVirtualMonitorDisplays(vmCount);
            panelCount = PanelCount(mode, physicals.Count, vmCount, vms.Count);
            physicals = _monitorProvider.GetCaptureSources()
                .OrderBy(m => m.X)
                .ThenBy(m => m.Y)
                .Take(panelCount)
                .ToList();

            vms = _monitorProvider.GetVirtualMonitorDisplays(vmCount);
            var required = RequiredVmCount(mode, physicals.Count);
            if (vms.Count < required)
            {
                var all = _monitorProvider.GetMonitors();
                var screenList = string.Join("; ", all.Select(m => $"{m.Name} {m.Width}x{m.Height}"));
                throw new InvalidOperationException(
                    $"Yetersiz sanal monitor. Gerekli: {required}, bulunan: {vms.Count}. Ekranlar: {screenList}");
            }

            var forms = BuildForms(physicals, vms, mode);
            _running = true;
            var started = new ManualResetEventSlim(false);
            Exception? startError = null;

            _uiThread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    _context = new MultiFormContext(forms);
                    started.Set();
                    Application.Run(_context);
                }
                catch (Exception ex)
                {
                    startError = ex;
                    started.Set();
                }
                finally
                {
                    _running = false;
                    _context = null;
                }
            })
            {
                IsBackground = true,
                Name = "VDisplay.PhysicalCompositor"
            };

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            started.Wait(TimeSpan.FromSeconds(5));

            if (startError is not null)
            {
                _running = false;
                throw new InvalidOperationException("Physical compositor baslatilamadi.", startError);
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_running)
            {
                return;
            }

            try
            {
                _context?.CloseAll();
            }
            catch
            {
                _running = false;
            }

            _uiThread?.Join(TimeSpan.FromSeconds(3));
            _uiThread = null;
            _context = null;
            _running = false;
        }
    }

    public void Dispose() => Stop();

    private static int PanelCount(PhysicalSplitMode mode, int physicalCount, int vmCount, int availableVmCount = int.MaxValue) =>
        mode switch
        {
            PhysicalSplitMode.HybridPhysicalVirtual => Math.Min(physicalCount, Math.Min(vmCount, availableVmCount)),
            PhysicalSplitMode.AllVirtualQuadrants => Math.Min(
                physicalCount,
                Math.Min(Math.Max(1, vmCount / 2), Math.Max(0, availableVmCount / 2))),
            _ => Math.Min(physicalCount, Math.Min(Math.Max(1, vmCount / 2), Math.Max(0, availableVmCount / 2)))
        };

    private static int RequiredVmCount(PhysicalSplitMode mode, int panelCount) =>
        mode switch
        {
            PhysicalSplitMode.HybridPhysicalVirtual => panelCount,
            PhysicalSplitMode.AllVirtualQuadrants => panelCount * 2,
            _ => panelCount * 2
        };

    private List<Form> BuildForms(
        IReadOnlyList<PhysicalMonitorInfo> physicals,
        IReadOnlyList<PhysicalMonitorInfo> vms,
        PhysicalSplitMode mode)
    {
        var forms = new List<Form>();
        var vmIndex = 0;
        Action onFrame = () => _ = Interlocked.Increment(ref _framesRendered);

        for (var i = 0; i < physicals.Count; i++)
        {
            var phys = physicals[i];
            var monitorNo = i + 1;
            var divider = new SplitDividerForm(phys);
            forms.Add(divider);

            if (mode == PhysicalSplitMode.HybridPhysicalVirtual)
            {
                var rightVm = vms[vmIndex++];
                var vmNo = monitorNo + 2;
                forms.Add(new PhysicalHalfForm(
                    phys,
                    RightHalfBounds(phys),
                    $"Ekran {monitorNo} Sag (VM{vmNo})",
                    [divider],
                    rightVm,
                    onFrame));
            }
            else
            {
                var leftVmNo = monitorNo * 2 + 1;
                var rightVmNo = monitorNo * 2 + 2;
                forms.Add(new PhysicalSplitForm(
                    phys,
                    $"Ekran {monitorNo} Sol (VM{leftVmNo})",
                    $"Ekran {monitorNo} Sag (VM{rightVmNo})",
                    [divider],
                    onFrame));
            }
        }

        return forms;
    }

    private static Rectangle RightHalfBounds(PhysicalMonitorInfo physical)
    {
        var halfW = Math.Max(1, physical.Width / 2);
        return new Rectangle(physical.X + halfW, physical.Y, physical.Width - halfW, physical.Height);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class MultiFormContext : ApplicationContext
{
    private readonly List<Form> _forms;

    public MultiFormContext(List<Form> forms)
    {
        _forms = forms;
        foreach (var form in forms)
        {
            form.FormClosed += OnFormClosed;
            form.Show();
        }
    }

    public void CloseAll()
    {
        foreach (var form in _forms.ToArray())
        {
            try
            {
                form.Close();
            }
            catch
            {
                // ignore
            }
        }
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (_forms.All(f => f.IsDisposed || !f.Visible))
        {
            ExitThread();
        }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class SplitDividerForm : Form
{
    public SplitDividerForm(PhysicalMonitorInfo physical)
    {
        Text = "VDisplay Divider";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        var x = physical.X + Math.Max(0, physical.Width / 2 - 1);
        Bounds = new Rectangle(x, physical.Y, 2, physical.Height);
        TopMost = true;
        BackColor = Color.FromArgb(220, 220, 220);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        Shown += (_, _) => { };
    }
}

[SupportedOSPlatform("windows")]
internal sealed class PhysicalHalfForm : Form
{
    private readonly PhysicalMonitorInfo _physical;
    private readonly PhysicalMonitorInfo? _vm;
    private readonly IReadOnlyList<Form> _hideForms;
    private readonly PictureBox _box;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Action _onFrame;

    public PhysicalHalfForm(
        PhysicalMonitorInfo physical,
        Rectangle bounds,
        string label,
        IReadOnlyList<Form> hideForms,
        PhysicalMonitorInfo? vm,
        Action onFrame)
    {
        _physical = physical;
        _vm = vm;
        _hideForms = hideForms.Append(this).ToList();
        _onFrame = onFrame;

        Text = "VDisplay Half";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        BackColor = Color.Black;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        var header = new Label
        {
            Text = label,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(120, 0, 0, 0),
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        _box = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Black
        };
        Controls.Add(_box);
        Controls.Add(header);

        _timer = new System.Windows.Forms.Timer { Interval = 33 };
        _timer.Tick += (_, _) =>
        {
            UpdateBox(
                _box,
                DisplayCaptureHelper.CaptureBehindOverlays(_physical, _hideForms, rightHalf: true, _vm));
            _onFrame();
        };
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _box.Image?.Dispose();
        base.OnFormClosed(e);
    }

    private static void UpdateBox(PictureBox box, Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        var old = box.Image;
        box.Image = (Bitmap)bitmap.Clone();
        old?.Dispose();
        bitmap.Dispose();
    }
}

[SupportedOSPlatform("windows")]
internal sealed class PhysicalSplitForm : Form
{
    private readonly PhysicalMonitorInfo _physical;
    private readonly IReadOnlyList<Form> _hideForms;
    private readonly PictureBox _leftBox;
    private readonly PictureBox _rightBox;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Action _onFrame;

    public PhysicalSplitForm(
        PhysicalMonitorInfo physical,
        string leftLabel,
        string rightLabel,
        IReadOnlyList<Form> hideForms,
        Action onFrame)
    {
        _physical = physical;
        _hideForms = hideForms.Append(this).ToList();
        _onFrame = onFrame;

        Text = $"VDisplay Split - {physical.Name}";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(physical.X, physical.Y, physical.Width, physical.Height);
        TopMost = true;
        BackColor = Color.Black;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 2f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        grid.Controls.Add(CreateHalfPanel(leftLabel, out _leftBox), 0, 0);
        grid.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(220, 220, 220) }, 1, 0);
        grid.Controls.Add(CreateHalfPanel(rightLabel, out _rightBox), 2, 0);
        Controls.Add(grid);

        _timer = new System.Windows.Forms.Timer { Interval = 33 };
        _timer.Tick += (_, _) => RenderFrame();
        _timer.Start();
    }

    private void RenderFrame()
    {
        var forms = _hideForms.Where(f => f is { IsDisposed: false, Visible: true }).ToList();
        foreach (var form in forms)
        {
            form.Hide();
        }

        try
        {
            Application.DoEvents();
            Thread.Sleep(1);
            UpdateBox(_leftBox, DisplayCaptureHelper.CaptureLeftHalf(_physical));
            UpdateBox(_rightBox, DisplayCaptureHelper.CaptureRightHalf(_physical));
        }
        finally
        {
            foreach (var form in forms)
            {
                if (!form.IsDisposed)
                {
                    form.Show();
                }
            }
        }

        _onFrame();
    }

    private static Panel CreateHalfPanel(string label, out PictureBox box)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        var header = new Label
        {
            Text = label,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(120, 0, 0, 0),
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        box = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Black
        };
        panel.Controls.Add(box);
        panel.Controls.Add(header);
        return panel;
    }

    private static void UpdateBox(PictureBox box, Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        var old = box.Image;
        box.Image = (Bitmap)bitmap.Clone();
        old?.Dispose();
        bitmap.Dispose();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _leftBox.Image?.Dispose();
        _rightBox.Image?.Dispose();
        base.OnFormClosed(e);
    }
}

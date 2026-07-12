using System.Runtime.Versioning;
using VDisplay.Overlay;

[assembly: SupportedOSPlatform("windows")]

ApplicationConfiguration.Initialize();
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";
var physicals = MonitorEnumerator.GetPhysicalMonitors();

if (physicals.Count == 0)
{
    MessageBox.Show("Fiziksel monitor bulunamadi.", "VDisplay", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return 1;
}

var physical = physicals[0];
var forms = new List<Form>();

if (mode == "hybrid")
{
    var divider = new DividerForm(physical);
    var hybrid = new HybridOverlayForm(physical);
    hybrid.CaptureAndApply();
    forms.Add(divider);
    forms.Add(hybrid);
}
else
{
    var split = new SplitOverlayForm(physical);
    split.CaptureAndApply();
    forms.Add(split);
}

using var ctx = new MultiFormAppContext(forms);
Application.Run(ctx);
return 0;

[SupportedOSPlatform("windows")]
file sealed class MultiFormAppContext : ApplicationContext
{
    public MultiFormAppContext(List<Form> forms)
    {
        foreach (var form in forms)
        {
            form.FormClosed += (_, _) =>
            {
                if (forms.All(f => f.IsDisposed || !f.Visible))
                {
                    ExitThread();
                }
            };
            form.Show();
        }
    }
}

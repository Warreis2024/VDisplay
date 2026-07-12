using VDisplay.Service;
using VDisplay.Service.Compositor;
using VDisplay.Service.Capture;
using VDisplay.Service.Driver;
using VDisplay.Service.Ipc;
using VDisplay.Service.Layout;
using VDisplay.Service.Monitor;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<PhysicalMonitorProvider>();
builder.Services.AddSingleton<LayoutManager>();
builder.Services.AddSingleton<SharedFrameBridge>();
builder.Services.AddSingleton<CaptureEngine>();
builder.Services.AddSingleton<DriverInstaller>();
builder.Services.AddSingleton<MonitorManager>();
builder.Services.AddSingleton<CaptureHostedService>();
builder.Services.AddSingleton<PhysicalCompositorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CaptureHostedService>());
builder.Services.AddHostedService<IpcHostedService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

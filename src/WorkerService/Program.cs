using WorkerService;
using WorkerService.Screenshot;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// Register screenshot services
builder.Services.AddSingleton<ScreenshotManager>();

var host = builder.Build();
host.Run();

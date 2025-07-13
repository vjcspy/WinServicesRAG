using Serilog;
using WinServicesRAG.Core.Screenshot;
using WorkerService;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(path: "logs/worker-service-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

    // Use Serilog as the logging provider
    builder.Services.AddSerilog();

    builder.Services.AddHostedService<Worker>();

    // Register screenshot services
    builder.Services.AddSingleton<IScreenshotManager, ScreenshotManager>();

    IHost host = builder.Build();

    Log.Information(messageTemplate: "WorkerService starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(exception: ex, messageTemplate: "WorkerService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

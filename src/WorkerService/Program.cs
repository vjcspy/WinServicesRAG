using WorkerService;
using WinServicesRAG.Core.Screenshot;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-service-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    
    // Use Serilog as the logging provider
    builder.Services.AddSerilog();
    
    builder.Services.AddHostedService<Worker>();

    // Register screenshot services
    builder.Services.AddSingleton<ScreenshotManager>();

    var host = builder.Build();
    
    Log.Information("WorkerService starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WorkerService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

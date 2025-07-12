using WatchdogService;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/watchdog-service-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    
    // Use Serilog as the logging provider
    builder.Services.AddSerilog();
    
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    
    Log.Information("WatchdogService starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WatchdogService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

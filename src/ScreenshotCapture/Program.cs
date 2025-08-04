using ScreenshotCapture.Commands;
using Serilog;
using System.CommandLine;
using System.Text;
using WinServicesRAG.Core.Configuration;

// Configure console to support Vietnamese characters
Console.OutputEncoding = Encoding.UTF8;

// Configure Serilog as global logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File($@"D:\Documents\Temporary\WinServicesRAG\logs\{WatchdogServiceConfig.ScreenshotCaptureLogFileName}-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("=== Windows System Monitoring Service ===");

    // Create and configure the root command
    RootCommand rootCommand = CommandSetup.CreateRootCommand();

    // Default to service mode if no command specified
    if (args.Length == 0)
    {
        args =
        [
            "service",
            "--hide-console"
        ];
    }

    // Parse and invoke
    await rootCommand.InvokeAsync(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}

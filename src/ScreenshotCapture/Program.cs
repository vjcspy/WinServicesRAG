using ScreenshotCapture.Commands;
using Serilog;
using System.CommandLine;
using System.Text;

// Configure console to support Vietnamese characters
Console.OutputEncoding = Encoding.UTF8;

// Configure Serilog as global logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(path: @"D:\Documents\Temporary\WinServicesRAG\logs\screenshot-capture-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information(messageTemplate: "=== ScreenshotCapture Independent Service ===");

    // Create and configure the root command
    RootCommand rootCommand = CommandSetup.CreateRootCommand();

    // Default to service mode if no command specified
    if (args.Length == 0)
    {
        args = new[]
        {
            "service",
            "--hide-console"
        };
    }

    // Parse and invoke
    await rootCommand.InvokeAsync(args: args);
}
catch (Exception ex)
{
    Log.Fatal(exception: ex, messageTemplate: "Application terminated unexpectedly");
    Environment.Exit(exitCode: 1);
}
finally
{
    Log.CloseAndFlush();
}

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WinServicesRAG.Core.Screenshot;
using ScreenshotCapture;

// Configure console to support Vietnamese characters
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Configure Serilog as global logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/screenshot-capture-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Console.WriteLine("=== ScreenshotCapture Independent Service ===");
    Console.WriteLine("A dedicated background service for capturing screenshots in user session");
    Console.WriteLine("Operates independently and parallel with WorkerService");
    Console.WriteLine("Managed by WatchdogService for high availability");
    Console.WriteLine();

    // P/Invoke for hiding console window
    const int SW_HIDE = 0;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // Create root command with service and CLI modes
    var rootCommand = new RootCommand("ScreenshotCapture - Independent background service for screenshot capture");

    // Service mode command
    var hideConsoleOption = new Option<bool>("--hide-console", "Hide console window for background operation");
    var workDirOption = new Option<string>("--work-dir", () => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\WinServicesRAG", "Working directory for job files");
    var pollIntervalOption = new Option<int>("--poll-interval", () => 5, "Job polling interval in seconds");
    var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

    var serviceCommand = new Command("service", "Run as background service");
    serviceCommand.AddOption(hideConsoleOption);
    serviceCommand.AddOption(workDirOption);
    serviceCommand.AddOption(pollIntervalOption);
    serviceCommand.AddOption(verboseOption);

    // CLI mode commands for testing
    var cliCommand = new Command("cli", "Run in CLI mode for testing");

    var outputOption = new Option<string>(
        name: "--output",
        description: "Output file path for the screenshot (PNG format)",
        getDefaultValue: () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"));

    var providerOption = new Option<string?>(
        name: "--provider",
        description: "Force specific screenshot provider");

    var statusOption = new Option<bool>(
        name: "--status",
        description: "Show status of all screenshot providers and exit");

    // Add CLI options
    cliCommand.AddOption(outputOption);
    cliCommand.AddOption(providerOption);
    cliCommand.AddOption(statusOption);
    cliCommand.AddOption(verboseOption);

    // Add commands to root
    rootCommand.AddCommand(serviceCommand);
    rootCommand.AddCommand(cliCommand);

    // Service mode handler with Host Builder
    serviceCommand.SetHandler(async (bool hideConsole, string workDir, int pollInterval, bool verbose) =>
    {
        try
        {
            // Hide console if requested
            if (hideConsole)
            {
                ShowWindow(GetConsoleWindow(), SW_HIDE);
            }

            // Create Host Builder with proper DI
            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register ScreenshotManager as singleton
                    services.AddSingleton<ScreenshotManager>();
                    
                    // Register background service with configuration
                    services.Configure<ScreenshotServiceConfig>(config =>
                    {
                        config.WorkDirectory = workDir;
                        config.PollIntervalSeconds = pollInterval;
                        config.Verbose = verbose;
                    });
                    services.AddHostedService<ScreenshotBackgroundService>();
                })
                .UseSerilog(); // Use Serilog as the logging provider

            var host = hostBuilder.Build();
            
            Log.Information("[ServiceMode] Starting ScreenshotCapture background service...");
            Log.Information("[ServiceMode] Working directory: {WorkDir}", workDir);
            Log.Information("[ServiceMode] Poll interval: {PollInterval} seconds", pollInterval);

            // Ensure working directory exists
            Directory.CreateDirectory(workDir);
            Directory.CreateDirectory(Path.Combine(workDir, "jobs"));
            Directory.CreateDirectory(Path.Combine(workDir, "screenshots"));
            Directory.CreateDirectory(Path.Combine(workDir, "results"));

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            Environment.Exit(1);
        }
    }, hideConsoleOption, workDirOption, pollIntervalOption, verboseOption);

    // CLI mode handler with Host Builder for consistency
    cliCommand.SetHandler(async (string output, string? provider, bool status, bool verbose) =>
    {
        try
        {
            // Create minimal host for DI
            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ScreenshotManager>();
                })
                .UseSerilog();

            var host = hostBuilder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var screenshotManager = host.Services.GetRequiredService<ScreenshotManager>();

            // Handle status command
            if (status)
            {
                logger.LogInformation("Checking provider status...");

                var providerStatus = screenshotManager.GetProviderStatus();

                Console.WriteLine("Screenshot Provider Status:");
                Console.WriteLine("===========================");

                foreach (var (name, isAvailable) in providerStatus)
                {
                    string statusText = isAvailable ? "✓ Available" : "✗ Not Available";
                    Console.WriteLine($"{name}: {statusText}");
                }

                Environment.Exit(0);
                return;
            }

            // Take screenshot
            logger.LogInformation("Taking screenshot with output: {Output}", output);

            byte[]? screenshotData;

            if (!string.IsNullOrEmpty(provider))
            {
                logger.LogInformation("Using forced provider: {Provider}", provider);
                screenshotData = screenshotManager.TakeScreenshotWithProvider(provider);
            }
            else
            {
                logger.LogInformation("Using automatic provider selection");
                screenshotData = screenshotManager.TakeScreenshot();
            }

            if (screenshotData == null || screenshotData.Length == 0)
            {
                logger.LogError("Failed to capture screenshot - all providers failed");
                Environment.Exit(1);
                return;
            }

            // Ensure output directory exists
            string? outputDir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                logger.LogInformation("Created output directory: {OutputDir}", outputDir);
            }

            // Save screenshot
            await File.WriteAllBytesAsync(output, screenshotData);

            logger.LogInformation("Screenshot saved successfully: {Output}", output);
            logger.LogInformation("File size: {Size:N0} bytes", screenshotData.Length);

            // Success exit code
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "CLI command failed");
            Environment.Exit(1);
        }
    }, outputOption, providerOption, statusOption, verboseOption);

    // Default to service mode if no command specified
    if (args.Length == 0)
    {
        args = new[] { "service", "--hide-console" };
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

public partial class Program { }

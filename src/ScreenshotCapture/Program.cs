using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenshotCapture;
using Serilog;
using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text;
using WinServicesRAG.Core.Screenshot;

// Configure console to support Vietnamese characters
Console.OutputEncoding = Encoding.UTF8;

// Configure Serilog as global logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    // .WriteTo.File(path: "logs/screenshot-capture-.log",
    //     rollingInterval: RollingInterval.Day,
    //     retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Console.WriteLine(value: "=== ScreenshotCapture Independent Service ===");
    Console.WriteLine(value: "A dedicated background service for capturing screenshots in user session");
    Console.WriteLine(value: "Operates independently and parallel with WorkerService");
    Console.WriteLine(value: "Managed by WatchdogService for high availability");
    Console.WriteLine();

    // P/Invoke for hiding console window
    const int SW_HIDE = 0;

    [DllImport(dllName: "kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport(dllName: "user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // Create root command with service and CLI modes
    var rootCommand = new RootCommand(description: "ScreenshotCapture - Independent background service for screenshot capture");

    // Service mode command
    var hideConsoleOption = new Option<bool>(name: "--hide-console", description: "Hide console window for background operation");
    var workDirOption = new Option<string>(name: "--work-dir", getDefaultValue: () => Environment.GetFolderPath(folder: Environment.SpecialFolder.CommonApplicationData) + "\\WinServicesRAG", description: "Working directory for job files");
    var pollIntervalOption = new Option<int>(name: "--poll-interval", getDefaultValue: () => 5, description: "Job polling interval in seconds");
    var verboseOption = new Option<bool>(name: "--verbose", description: "Enable verbose logging");

    var serviceCommand = new Command(name: "service", description: "Run as background service");
    serviceCommand.AddOption(option: hideConsoleOption);
    serviceCommand.AddOption(option: workDirOption);
    serviceCommand.AddOption(option: pollIntervalOption);
    serviceCommand.AddOption(option: verboseOption);

    // CLI mode commands for testing
    var cliCommand = new Command(name: "cli", description: "Run in CLI mode for testing");

    var outputOption = new Option<string>(
        name: "--output",
        description: "Output file path for the screenshot (PNG format)",
        getDefaultValue: () => Path.Combine(path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.Desktop),
            path2: $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"));

    var providerOption = new Option<string?>(
        name: "--provider",
        description: "Force specific screenshot provider");

    var statusOption = new Option<bool>(
        name: "--status",
        description: "Show status of all screenshot providers and exit");

    // Add CLI options
    cliCommand.AddOption(option: outputOption);
    cliCommand.AddOption(option: providerOption);
    cliCommand.AddOption(option: statusOption);
    cliCommand.AddOption(option: verboseOption);

    // Add commands to root
    rootCommand.AddCommand(command: serviceCommand);
    rootCommand.AddCommand(command: cliCommand);

    // Service mode handler with Host Builder
    serviceCommand.SetHandler(handle: async (hideConsole, workDir, pollInterval, verbose) =>
    {
        try
        {
            // Hide console if requested
            if (hideConsole)
            {
                ShowWindow(hWnd: GetConsoleWindow(), nCmdShow: SW_HIDE);
            }

            // Create Host Builder with proper DI
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(configureDelegate: (context, services) =>
                {
                    // Override the screenshot manager registration with a working factory
                    services.AddSingleton<IScreenshotManager, ScreenshotManager>();

                    // Register background service with configuration
                    services.Configure<ScreenshotServiceConfig>(configureOptions: config =>
                    {
                        config.WorkDirectory = workDir;
                        config.PollIntervalSeconds = pollInterval;
                        config.Verbose = verbose;
                    });
                    services.AddHostedService<ScreenshotBackgroundService>();
                })
                .UseSerilog(); // Use Serilog as the logging provider

            IHost host = hostBuilder.Build();

            Log.Information(messageTemplate: "[ServiceMode] Starting ScreenshotCapture background service...");
            Log.Information(messageTemplate: "[ServiceMode] Working directory: {WorkDir}", propertyValue: workDir);
            Log.Information(messageTemplate: "[ServiceMode] Poll interval: {PollInterval} seconds", propertyValue: pollInterval);

            // Ensure working directory exists
            Directory.CreateDirectory(path: workDir);
            Directory.CreateDirectory(path: Path.Combine(path1: workDir, path2: "jobs"));
            Directory.CreateDirectory(path: Path.Combine(path1: workDir, path2: "screenshots"));
            Directory.CreateDirectory(path: Path.Combine(path1: workDir, path2: "results"));

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(exception: ex, messageTemplate: "Host terminated unexpectedly");
            Environment.Exit(exitCode: 1);
        }
    }, symbol1: hideConsoleOption, symbol2: workDirOption, symbol3: pollIntervalOption, symbol4: verboseOption);


    // CLI mode handler with Host Builder for consistency
    cliCommand.SetHandler(handle: async (output, provider, status, verbose) =>
    {
        try
        {
            // Create minimal host for DI
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(configureDelegate: (context, services) =>
                {
                    services.AddSingleton<IScreenshotManager, ScreenshotManager>();
                })
                .UseSerilog();

            IHost host = hostBuilder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            IScreenshotManager screenshotManager = host.Services.GetRequiredService<IScreenshotManager>();

            // Handle status command
            if (status)
            {
                logger.LogInformation(message: "Checking provider status...");

                var providerStatus = screenshotManager.GetProviderStatus();

                Console.WriteLine(value: "Screenshot Provider Status:");
                Console.WriteLine(value: "===========================");

                foreach (var kvp in providerStatus)
                {
                    string name = kvp.Key;
                    bool isAvailable = kvp.Value;
                    string statusText = isAvailable ? "✓ Available" : "✗ Not Available";
                    Console.WriteLine(value: $"{name}: {statusText}");
                }

                Environment.Exit(exitCode: 0);
                return;
            }

            // Take screenshot
            logger.LogInformation(message: "Taking screenshot with output: {Output}", output);

            ScreenshotResult screenshotResult;

            if (!string.IsNullOrEmpty(value: provider))
            {
                logger.LogInformation(message: "Using forced provider: {Provider}", provider);
                screenshotResult = await screenshotManager.TakeScreenshotAsync(providerName: provider);
            }
            else
            {
                logger.LogInformation(message: "Using automatic provider selection");
                screenshotResult = await screenshotManager.TakeScreenshotAsync();
            }

            if (!screenshotResult.Success || screenshotResult.ImageData == null || screenshotResult.ImageData.Length == 0)
            {
                logger.LogError(message: "Failed to capture screenshot: {Error}", screenshotResult.ErrorMessage ?? "all providers failed");
                Environment.Exit(exitCode: 1);
                return;
            }

            // Ensure output directory exists
            string? outputDir = Path.GetDirectoryName(path: output);
            if (!string.IsNullOrEmpty(value: outputDir) && !Directory.Exists(path: outputDir))
            {
                Directory.CreateDirectory(path: outputDir);
                logger.LogInformation(message: "Created output directory: {OutputDir}", outputDir);
            }

            // Save screenshot
            await File.WriteAllBytesAsync(path: output, bytes: screenshotResult.ImageData);

            logger.LogInformation(message: "Screenshot saved successfully: {Output}", output);
            logger.LogInformation(message: "File size: {Size:N0} bytes", screenshotResult.ImageData.Length);

            // Success exit code
            Environment.Exit(exitCode: 0);
        }
        catch (Exception ex)
        {
            Log.Fatal(exception: ex, messageTemplate: "CLI command failed");
            Environment.Exit(exitCode: 1);
        }
    }, symbol1: outputOption, symbol2: providerOption, symbol3: statusOption, symbol4: verboseOption);

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

public partial class Program
{
}

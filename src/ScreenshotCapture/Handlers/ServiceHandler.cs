using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Runtime.InteropServices;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Helper;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Processing;
using WinServicesRAG.Core.Services;
using WinServicesRAG.Core.Value;
namespace ScreenshotCapture.Handlers;

public static class ServiceHandler
{
    // P/Invoke for hiding console window
    private const int SW_HIDE = 0;

    [DllImport(dllName: "kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport(dllName: "user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static async Task HandleServiceMode(bool hideConsole, string workDir, int pollInterval, bool verbose)
    {
        try
        {
            // Set runtime mode for service
            RuntimeDataHelper.SetData(key: CommonValue.RUNTIME_MODE_KEY, value: CommonValue.RUNTIME_MODE_SERVICE);
            
            // Hide console if requested
            if (hideConsole)
            {
                ShowWindow(hWnd: GetConsoleWindow(), nCmdShow: SW_HIDE);
            }

            // Create Host Builder with proper DI
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(configureDelegate: (context, services) =>
                {
                    // Register core services (same as CliHandler)
                    services.AddSingleton<IScreenshotManager, ScreenshotManager>();
                    services.AddSingleton<IJobProcessingEngine, ScreenshotJobProcessingEngine>();

                    // Configure API client options
                    services.Configure<ApiClientOptions>(config: context.Configuration.GetSection(key: ApiClientOptions.SectionName));
                    services.AddSingleton<IApiClient, ApiClient>();
                    services.AddHttpClient<ApiClient>(); // Register HttpClient for ApiClient

                    // Register background service with configuration
                    services.Configure<ScreenshotServiceConfig>(configureOptions: config =>
                    {
                        config.WorkDirectory = workDir;
                        config.PollIntervalSeconds = pollInterval;
                        config.Verbose = verbose;
                    });
                    services.AddHostedService<ScreenshotBackgroundService>();
                })
                .UseWindowsService() // Enable Windows Service support
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
    }
}

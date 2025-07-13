using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Runtime.InteropServices;
using WinServicesRAG.Core.Screenshot;
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
    }
}

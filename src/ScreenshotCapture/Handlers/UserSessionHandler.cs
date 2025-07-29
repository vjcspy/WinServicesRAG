using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WinServicesRAG.Core.Helper;
using WinServicesRAG.Core.Observer;
using WinServicesRAG.Core.Processing;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Services;
using WinServicesRAG.Core.Value;
using WinServicesRAG.Core.Models;

namespace ScreenshotCapture.Handlers;

public static class UserSessionHandler
{
    public static async Task HandleUserSessionMode(int sessionId, bool verbose)
    {
        try
        {
            RuntimeDataHelper.SetData(key: CommonValue.RUNTIME_MODE_KEY, value: "USER_SESSION");
            RuntimeDataHelper.SetData("SESSION_ID", sessionId.ToString());

            // Configure enhanced logging for user session mode
            // if (verbose)
            // {
            //     Log.Logger = new LoggerConfiguration()
            //         .MinimumLevel.Debug()
            //         .WriteTo.Console()
            //         .WriteTo.File(path: @$"D:\Documents\Temporary\WinServicesRAG\logs\service-session{sessionId}-.log",
            //             rollingInterval: RollingInterval.Day,
            //             retainedFileCountLimit: 7)
            //         .CreateLogger();
            // }

            Log.Information("=== ScreenshotCapture User Session Mode ===");
            Log.Information("Session ID: {SessionId}", sessionId);
            Log.Information("Process ID: {ProcessId}", Environment.ProcessId);
            Log.Information("Current User: {UserName}", Environment.UserName);
            Log.Information("Machine Name: {MachineName}", Environment.MachineName);

            // Create host for user session mode
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(configureDelegate: (context, services) =>
                {
                    services.AddSingleton<IScreenshotManager, ScreenshotManager>();
                    services.AddSingleton<IImageCompressionService, ImageCompressionService>();
                    services.AddSingleton<IJobProcessingEngine, ScreenshotJobProcessingEngine>();

                    // Configure API client
                    services.Configure<ApiClientOptions>(config: context.Configuration.GetSection(key: ApiClientOptions.SectionName));
                    services.AddSingleton<IApiClient, ApiClient>();
                    services.AddHttpClient<ApiClient>();
                })
                .UseSerilog();

            IHost host = hostBuilder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            IJobProcessingEngine jobProcessingEngine = host.Services.GetRequiredService<IJobProcessingEngine>();
            IScreenshotManager screenshotManager = host.Services.GetRequiredService<IScreenshotManager>();

            // Show provider status for user session
            // await ShowProviderStatusInUserSession(logger, screenshotManager, sessionId);

            // Start job processing in user session mode
            logger.LogInformation("Starting job processing in user session {SessionId}...", sessionId);
            
            var subscription = jobProcessingEngine.ProcessingResults.Subscribe(
                observer: new JobResultObserver(logger: logger));

            jobProcessingEngine.Start();

            // Register shutdown handlers
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Shutdown requested for user session mode");
                cts.Cancel();
            };

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                logger.LogInformation("Process exit detected for user session mode");
                cts.Cancel();
            };

            // Keep running until cancellation
            // logger.LogInformation("User session mode is running. Press Ctrl+C to stop.");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("User session mode shutdown initiated");
            }

            // Cleanup
            jobProcessingEngine.Stop();
            subscription.Dispose();
            logger.LogInformation("User session mode stopped");
        }
        catch (Exception ex)
        {
            Log.Fatal(exception: ex, messageTemplate: "User session mode failed");
            Environment.Exit(exitCode: 1);
        }
    }

    private static async Task ShowProviderStatusInUserSession(Microsoft.Extensions.Logging.ILogger logger, IScreenshotManager screenshotManager, int sessionId)
    {
        logger.LogInformation("Checking screenshot provider status in session {SessionId}...", sessionId);

        var providerStatus = screenshotManager.GetProviderStatus();

        Log.Information("Screenshot Provider Status (Session {SessionId}):", sessionId);
        Log.Information("=============================================");

        foreach (var kvp in providerStatus)
        {
            string name = kvp.Key;
            bool isAvailable = kvp.Value;
            string statusText = isAvailable ? "✓ Available" : "✗ Not Available";
            Log.Information("{ProviderName}: {Status}", name, statusText);
        }

        // Take a test screenshot to verify functionality
        Log.Information("Testing screenshot capture in user session...");
        
        try
        {
            var testResult = await screenshotManager.TakeScreenshotAsync();
            if (testResult.Success && testResult.ImageData?.Length > 0)
            {
                Log.Information("✓ Screenshot test successful: {Size:N0} bytes using {Provider}",
                    testResult.ImageData.Length, testResult.ProviderUsed ?? "unknown");
            }
            else
            {
                Log.Warning("✗ Screenshot test failed: {Error}", testResult.ErrorMessage ?? "unknown error");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Screenshot test failed with exception");
        }
    }
}

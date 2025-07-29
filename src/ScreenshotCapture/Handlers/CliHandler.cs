using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WinServicesRAG.Core.Helper;
using WinServicesRAG.Core.Models;
using WinServicesRAG.Core.Observer;
using WinServicesRAG.Core.Processing;
using WinServicesRAG.Core.Screenshot;
using WinServicesRAG.Core.Services;
using WinServicesRAG.Core.Value;
namespace ScreenshotCapture.Handlers;

public static class CliHandler
{
    public static async Task HandleCliMode(string action, string output, string provider, bool status, bool verbose)
    {
        try
        {
            RuntimeDataHelper.SetData(key: CommonValue.RUNTIME_MODE_KEY, value: "CLI");
            // Create minimal host for DI
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureServices(configureDelegate: (context, services) =>
                {
                    services.AddSingleton<IScreenshotManager, ScreenshotManager>();
                    services.AddSingleton<IImageCompressionService, ImageCompressionService>();
                    services.AddSingleton<IJobProcessingEngine, ScreenshotJobProcessingEngine>();

                    // THAY ĐỔI: Cách đúng để bind configuration
                    services.Configure<ApiClientOptions>(config: context.Configuration.GetSection(key: ApiClientOptions.SectionName));
                    services.AddSingleton<IApiClient, ApiClient>();
                    services.AddHttpClient<ApiClient>(); // Đăng ký HttpClient cho ApiClient
                })
                .UseSerilog();

            IHost host = hostBuilder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            IScreenshotManager screenshotManager = host.Services.GetRequiredService<IScreenshotManager>();
            IJobProcessingEngine jobProcessingEngine = host.Services.GetRequiredService<IJobProcessingEngine>();

            // Handle status command
            if (status)
            {
                await ShowProviderStatus(logger: logger, screenshotManager: screenshotManager);
                return;
            }

            switch (action)
            {
                case "screenshot":
                    await TakeScreenshot(logger: logger, screenshotManager: screenshotManager, output: output, provider: provider);
                    break;
                case "poll":
                    logger.LogInformation(message: "Started polling for screenshots...");
                    jobProcessingEngine.ProcessingResults.Subscribe(
                        observer: new JobResultObserver(logger: logger));
                    jobProcessingEngine.Start();
                    Console.ReadKey();
                    jobProcessingEngine.Stop();
                    break;
                default:
                    logger.LogInformation(message: "Invalid action specified. Use 'screenshot' or 'poll'.");
                    break;
            }

        }
        catch (Exception ex)
        {
            Log.Fatal(exception: ex, messageTemplate: "CLI command failed");
            Environment.Exit(exitCode: 1);
        }
    }

    private static Task ShowProviderStatus(ILogger<Program> logger, IScreenshotManager screenshotManager)
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
        return Task.CompletedTask;
    }

    private static async Task TakeScreenshot(ILogger<Program> logger, IScreenshotManager screenshotManager, string output, string? provider)
    {
        logger.LogInformation(message: "Taking screenshot with output directory: {Output}", output);

        ScreenshotResult screenshotResult;
        string actualProvider;

        if (!string.IsNullOrEmpty(value: provider))
        {
            logger.LogInformation(message: "Using forced provider: {Provider}", provider);
            screenshotResult = await screenshotManager.TakeScreenshotAsync(providerName: provider);
            actualProvider = provider;
        }
        else
        {
            logger.LogInformation(message: "Using automatic provider selection");
            screenshotResult = await screenshotManager.TakeScreenshotAsync();
            actualProvider = screenshotResult.ProviderUsed ?? "unknown";
        }

        if (!screenshotResult.Success || screenshotResult.ImageData == null || screenshotResult.ImageData.Length == 0)
        {
            logger.LogError(message: "Failed to capture screenshot: {Error}", screenshotResult.ErrorMessage ?? "all providers failed");
            Environment.Exit(exitCode: 1);
            return;
        }

        // Ensure output directory exists
        if (!Directory.Exists(path: output))
        {
            Directory.CreateDirectory(path: output);
            logger.LogInformation(message: "Created output directory: {OutputDir}", output);
        }

        // Generate filename with provider and datetime
        var timestamp = DateTime.Now.ToString(format: "yyyyMMdd_HHmmss");
        var filename = $"{actualProvider}_{timestamp}.png";
        string fullPath = Path.Combine(path1: output, path2: filename);

        // Save screenshot
        await File.WriteAllBytesAsync(path: fullPath, bytes: screenshotResult.ImageData);

        logger.LogInformation(message: "Screenshot saved successfully: {Output}", fullPath);
        logger.LogInformation(message: "File size: {Size:N0} bytes", screenshotResult.ImageData.Length);

        // Success exit code
        Environment.Exit(exitCode: 0);
    }
}

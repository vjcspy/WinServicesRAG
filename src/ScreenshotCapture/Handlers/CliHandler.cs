using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WinServicesRAG.Core.Screenshot;
namespace ScreenshotCapture.Handlers;

public static class CliHandler
{
    public static async Task HandleCliMode(string output, string? provider, bool status, bool verbose)
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
                await ShowProviderStatus(logger: logger, screenshotManager: screenshotManager);
                return;
            }

            // Take screenshot
            await TakeScreenshot(logger: logger, screenshotManager: screenshotManager, output: output, provider: provider);
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
}

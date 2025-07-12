using System.CommandLine;
using WinServicesRAG.Core.Screenshot;

// Configure console to support Vietnamese characters
Console.OutputEncoding = System.Text.Encoding.UTF8;

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

// Service mode handler
serviceCommand.SetHandler(async (bool hideConsole, string workDir, int pollInterval, bool verbose) =>
{
    try
    {
        // Hide console if requested
        if (hideConsole)
        {
            ShowWindow(GetConsoleWindow(), SW_HIDE);
        }

        // Configure logging level
        if (!verbose)
        {
            Console.SetOut(TextWriter.Null);
        }

        Console.WriteLine("[ServiceMode] Starting ScreenshotCapture background service...");
        Console.WriteLine($"[ServiceMode] Working directory: {workDir}");
        Console.WriteLine($"[ServiceMode] Poll interval: {pollInterval} seconds");

        // Ensure working directory exists
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(Path.Combine(workDir, "jobs"));
        Directory.CreateDirectory(Path.Combine(workDir, "screenshots"));

        using var screenshotManager = new ScreenshotManager();

        // Service main loop
        using var cancellationTokenSource = new CancellationTokenSource();
        
        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        Console.WriteLine("[ServiceMode] Background service started. Monitoring for screenshot jobs...");

        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await ProcessScreenshotJobs(workDir, screenshotManager, verbose);
                await Task.Delay(TimeSpan.FromSeconds(pollInterval), cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceMode] Error processing jobs: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(pollInterval), cancellationTokenSource.Token);
            }
        }

        Console.WriteLine("[ServiceMode] Background service stopped.");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
        if (verbose)
        {
            Console.Error.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        Environment.Exit(1);
    }
}, hideConsoleOption, workDirOption, pollIntervalOption, verboseOption);

// CLI mode handler (original functionality)
cliCommand.SetHandler(async (string output, string? provider, bool status, bool verbose) =>
{
    try
    {
        // Configure logging level
        if (!verbose)
        {
            Console.SetOut(TextWriter.Null);
        }

        using var screenshotManager = new ScreenshotManager();

        // Handle status command
        if (status)
        {
            if (verbose) Console.WriteLine("Checking provider status...");
            
            var providerStatus = screenshotManager.GetProviderStatus();
            
            // Re-enable output for status display
            if (!verbose)
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
            
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
        if (verbose) Console.WriteLine($"Taking screenshot with output: {output}");
        
        byte[]? screenshotData;
        
        if (!string.IsNullOrEmpty(provider))
        {
            if (verbose) Console.WriteLine($"Using forced provider: {provider}");
            screenshotData = screenshotManager.TakeScreenshotWithProvider(provider);
        }
        else
        {
            if (verbose) Console.WriteLine("Using automatic provider selection");
            screenshotData = screenshotManager.TakeScreenshot();
        }

        if (screenshotData == null || screenshotData.Length == 0)
        {
            Console.Error.WriteLine("ERROR: Failed to capture screenshot - all providers failed");
            Environment.Exit(1);
            return;
        }

        // Ensure output directory exists
        string? outputDir = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            if (verbose) Console.WriteLine($"Created output directory: {outputDir}");
        }

        // Save screenshot
        await File.WriteAllBytesAsync(output, screenshotData);
        
        if (verbose) 
        {
            Console.WriteLine($"Screenshot saved successfully: {output}");
            Console.WriteLine($"File size: {screenshotData.Length:N0} bytes");
        }

        // Success exit code
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
        if (verbose)
        {
            Console.Error.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
        }
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

// Background service job processing logic
static async Task ProcessScreenshotJobs(string workDir, ScreenshotManager screenshotManager, bool verbose)
{
    var jobsDir = Path.Combine(workDir, "jobs");
    var screenshotsDir = Path.Combine(workDir, "screenshots");

    // Look for pending job files
    var jobFiles = Directory.GetFiles(jobsDir, "*.job", SearchOption.TopDirectoryOnly);
    
    foreach (var jobFile in jobFiles)
    {
        try
        {
            if (verbose) Console.WriteLine($"[ServiceMode] Processing job file: {Path.GetFileName(jobFile)}");

            // Read job details (JSON format expected)
            var jobContent = await File.ReadAllTextAsync(jobFile);
            
            // Simple parsing - in real implementation, use proper JSON deserialization
            var lines = jobContent.Split('\n');
            var jobId = ExtractValue(lines, "job_id");
            var outputFile = Path.Combine(screenshotsDir, $"{jobId}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            if (verbose) Console.WriteLine($"[ServiceMode] Taking screenshot for job: {jobId}");

            // Take screenshot
            var screenshotData = screenshotManager.TakeScreenshot();

            if (screenshotData != null && screenshotData.Length > 0)
            {
                // Save screenshot
                await File.WriteAllBytesAsync(outputFile, screenshotData);
                
                // Create result file
                var resultFile = Path.Combine(workDir, "results", $"{jobId}.result");
                Directory.CreateDirectory(Path.GetDirectoryName(resultFile)!);
                
                await File.WriteAllTextAsync(resultFile, $@"{{
    ""job_id"": ""{jobId}"",
    ""status"": ""SUCCESS"",
    ""screenshot_path"": ""{outputFile}"",
    ""file_size"": {screenshotData.Length},
    ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
}}");

                if (verbose) Console.WriteLine($"[ServiceMode] Screenshot completed: {outputFile} ({screenshotData.Length:N0} bytes)");
            }
            else
            {
                // Create error result file
                var resultFile = Path.Combine(workDir, "results", $"{jobId}.result");
                Directory.CreateDirectory(Path.GetDirectoryName(resultFile)!);
                
                await File.WriteAllTextAsync(resultFile, $@"{{
    ""job_id"": ""{jobId}"",
    ""status"": ""ERROR"",
    ""error_message"": ""Failed to capture screenshot - all providers failed"",
    ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}""
}}");

                if (verbose) Console.WriteLine($"[ServiceMode] Screenshot failed for job: {jobId}");
            }

            // Remove processed job file
            File.Delete(jobFile);
        }
        catch (Exception ex)
        {
            if (verbose) Console.WriteLine($"[ServiceMode] Error processing job file {jobFile}: {ex.Message}");
        }
    }
}

static string ExtractValue(string[] lines, string key)
{
    var line = lines.FirstOrDefault(l => l.Trim().StartsWith($"\"{key}\""));
    if (line != null)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
        {
            var value = line.Substring(colonIndex + 1).Trim().Trim('"', ',');
            return value;
        }
    }
    return string.Empty;
}

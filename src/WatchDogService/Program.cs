using Serilog;
using Serilog.Events;
using System.Reflection;
using WinServicesRAG.Core.Configuration;
using WinServicesRAG.Core.Process;
using WinServicesRAG.Core.Session;
namespace WatchdogService;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure minimal early logging for startup
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Starting WatchdogService host...");

            IHost host = CreateHostBuilder(args).Build();

            // Validate configuration on startup
            await ValidateConfigurationAsync(host.Services);

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WatchdogService host terminated unexpectedly");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWindowsService(configure: options =>
            {
                options.ServiceName = "WatchdogService";
            })
            .ConfigureAppConfiguration(configureDelegate: (context, config) =>
            {
                // Add configuration sources
                config.AddJsonFile("appsettings.json", false, true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    true, true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices(configureDelegate: (context, services) =>
            {
                // Configure WatchdogServiceConfig
                services.Configure<WatchdogServiceConfig>(
                    context.Configuration.GetSection("WatchdogService"));

                // Register core services
                services.AddSingleton<ISessionManager, SessionManager>();
                services.AddSingleton<IUserSessionProcessLauncher, UserSessionProcessLauncher>();

                // Register the main service
                services.AddHostedService<WatchdogService>();
            })
            .UseSerilog(configureLogger: (context, services, configuration) =>
            {
                // Use the specific log path requested
                string logPath = @"D:\Documents\Temporary\WinServicesRAG\logs";
                Directory.CreateDirectory(logPath);

                configuration
                    .ReadFrom.Services(services)
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "WatchdogService")
                    .Enrich.WithProperty("Version", GetAssemblyVersion())
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        Path.Combine(logPath, "watchdog-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

                // Add EventLog only on Windows
                if (OperatingSystem.IsWindows())
                {
                    configuration.WriteTo.EventLog(
                        "WatchdogService",
                        "Application",
                        manageEventSource: true,
                        restrictedToMinimumLevel: LogEventLevel.Warning);
                }
            });
    }

    private static Task ValidateConfigurationAsync(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        try
        {
            // Validate WatchdogServiceConfig
            IConfigurationSection configSection = configuration.GetSection("WatchdogService");
            if (!configSection.Exists())
            {
                throw new InvalidOperationException("WatchdogService configuration section not found");
            }

            var config = new WatchdogServiceConfig();
            configSection.Bind(config);

            var validationErrors = config.Validate();
            if (validationErrors.Any())
            {
                throw new InvalidOperationException(
                    $"Configuration validation failed:\n{string.Join("\n", validationErrors)}");
            }

            logger.LogInformation("Configuration validation successful");
            logger.LogInformation("Configuration summary:\n{ConfigSummary}", config.GetConfigurationSummary());

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Configuration validation failed");
            throw;
        }
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
    }
}

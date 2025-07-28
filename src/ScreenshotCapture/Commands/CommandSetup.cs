using ScreenshotCapture.Handlers;
using System.CommandLine;
namespace ScreenshotCapture.Commands;

public static class CommandSetup
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand(description: "ScreenshotCapture - Independent background service for screenshot capture");

        // Add service and CLI commands
        rootCommand.AddCommand(command: CreateServiceCommand());
        rootCommand.AddCommand(command: CreateCliCommand());
        rootCommand.AddCommand(command: UserSessionCommand.CreateUserSessionCommand());

        return rootCommand;
    }

    private static Command CreateServiceCommand()
    {
        var serviceCommand = new Command(name: "service", description: "Run as background service");

        // Service options
        var hideConsoleOption = new Option<bool>(name: "--hide-console", description: "Hide console window for background operation");
        var workDirOption = new Option<string>(name: "--work-dir", getDefaultValue: () => Environment.GetFolderPath(folder: Environment.SpecialFolder.CommonApplicationData) + "\\WinServicesRAG", description: "Working directory for job files");
        var pollIntervalOption = new Option<int>(name: "--poll-interval", getDefaultValue: () => 5, description: "Job polling interval in seconds");
        var verboseOption = new Option<bool>(name: "--verbose", description: "Enable verbose logging");

        serviceCommand.AddOption(option: hideConsoleOption);
        serviceCommand.AddOption(option: workDirOption);
        serviceCommand.AddOption(option: pollIntervalOption);
        serviceCommand.AddOption(option: verboseOption);

        // Set handler
        serviceCommand.SetHandler(handle: ServiceHandler.HandleServiceMode,
            symbol1: hideConsoleOption,
            symbol2: workDirOption,
            symbol3: pollIntervalOption,
            symbol4: verboseOption);

        return serviceCommand;
    }

    private static Command CreateCliCommand()
    {
        var cliCommand = new Command(name: "cli", description: "Run in CLI mode for testing");

        // CLI options
        var actionOption = new Option<string>(
            name: "--action",
            description: "Action to perform",
            getDefaultValue: () => "screenshot"
        ).FromAmong("screenshot", "poll");

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output file path for the screenshot (PNG format)",
            getDefaultValue: () => Path.Combine(@"D:\Documents\Temporary\WinServicesRAG\screenshots"));

        var providerOption = new Option<string>(
            name: "--provider",
            getDefaultValue: () => "DirectX",
            description: "Force specific screenshot provider");

        var statusOption = new Option<bool>(
            name: "--status",
            description: "Show status of all screenshot providers and exit");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose logging");

        cliCommand.AddOption(option: outputOption);
        cliCommand.AddOption(option: providerOption);
        cliCommand.AddOption(option: statusOption);
        cliCommand.AddOption(option: verboseOption);
        cliCommand.AddOption(option: actionOption);

        // Set handler
        cliCommand.SetHandler(handle: CliHandler.HandleCliMode,
            symbol1: actionOption,
            symbol2: outputOption,
            symbol3: providerOption,
            symbol4: statusOption,
            symbol5: verboseOption);

        return cliCommand;
    }
}

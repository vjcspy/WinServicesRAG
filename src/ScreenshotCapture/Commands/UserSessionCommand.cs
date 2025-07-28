using System.CommandLine;
using ScreenshotCapture.Handlers;

namespace ScreenshotCapture.Commands;

public static class UserSessionCommand
{
    public static Command CreateUserSessionCommand()
    {
        var userSessionCommand = new Command("user-session", "Run in user session mode (managed by WatchdogService)");

        var sessionIdOption = new Option<int>(
            name: "--session-id",
            description: "Target session ID to run in")
        {
            IsRequired = true
        };

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose logging");

        userSessionCommand.AddOption(sessionIdOption);
        userSessionCommand.AddOption(verboseOption);

        userSessionCommand.SetHandler(UserSessionHandler.HandleUserSessionMode, sessionIdOption, verboseOption);

        return userSessionCommand;
    }
}

using Microsoft.Extensions.Options;
using WinServicesRAG.Core.Configuration;
using WinServicesRAG.Core.Process;
using WinServicesRAG.Core.Session;
namespace WatchdogService;

/// <summary>
///     Enhanced Watchdog Service that manages ScreenshotCapture processes in user sessions
///     Implements Interactive Service pattern for desktop screenshot access
/// </summary>
public class WatchdogService(
    ILogger<WatchdogService> logger,
    IOptions<WatchdogServiceConfig> config,
    ISessionManager sessionManager,
    IUserSessionProcessLauncher processLauncher)
    : BackgroundService, IDisposable
{
    private readonly WatchdogServiceConfig _config = config.Value;

    private readonly Lock _lockObject = new Lock();
    private readonly Dictionary<int, int> _restartAttempts = new Dictionary<int, int>();

    // Track managed processes per session
    private readonly Dictionary<int, ProcessInfo> _userProcesses = new Dictionary<int, ProcessInfo>();
    
    // Semaphore to prevent concurrent process launches
    private readonly SemaphoreSlim _launchSemaphore = new SemaphoreSlim(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Enhanced Watchdog Service starting up...");

        // Validate configuration
        var validationErrors = _config.Validate();
        if (validationErrors.Any())
        {
            logger.LogError("Configuration validation failed:\n{Errors}", string.Join("\n", validationErrors));
            throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }

        // Log configuration summary
        logger.LogInformation("Configuration:\n{ConfigSummary}", _config.GetConfigurationSummary());

        try
        {
            // Subscribe to session change events
            sessionManager.SessionChanged += OnSessionChanged;

            // Start session monitoring
            await sessionManager.StartMonitoringAsync();
            logger.LogInformation("Session monitoring started");

            // Initial scan for active sessions
            await ScanAndManageActiveSessionsAsync();

            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorAndMaintainProcessesAsync();
                    await Task.Delay(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in main monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Watchdog Service cancellation requested");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in Watchdog Service");
            throw;
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private async void OnSessionChanged(object? sender, SessionEventArgs e)
    {
        try
        {
            logger.LogInformation("Session change detected: {ChangeType} for Session {SessionId} ({UserName})",
                e.ChangeType, e.SessionInfo.SessionId, e.SessionInfo.UserName);

            switch (e.ChangeType)
            {
                case SessionChangeType.SessionLogon:
                case SessionChangeType.SessionRemoteConnect:
                    await HandleSessionLogonAsync(e.SessionInfo);
                    break;

                case SessionChangeType.SessionLogoff:
                case SessionChangeType.SessionRemoteDisconnect:
                    await HandleSessionLogoffAsync(e.SessionInfo);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling session change event");
        }
    }

    private async Task ScanAndManageActiveSessionsAsync()
    {
        try
        {
            var activeSessions = await sessionManager.GetActiveUserSessionsAsync();
            logger.LogInformation("Found {Count} active user sessions", activeSessions.Count);

            foreach (SessionInfo session in activeSessions)
            {
                logger.LogInformation("Active session: {SessionId} - {UserName}@{Domain} ({State})",
                    session.SessionId, session.UserName, session.Domain, session.State);

                if (ShouldManageSession(session))
                {
                    await EnsureScreenshotProcessAsync(session.SessionId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scanning active sessions");
        }
    }

    private async Task MonitorAndMaintainProcessesAsync()
    {
        await Task.Delay(100);
        lock (_lockObject)
        {
            var sessionsToCheck = _userProcesses.Keys.ToList();

            foreach (int sessionId in sessionsToCheck)
            {
                ProcessInfo processInfo = _userProcesses[sessionId];

                if (processLauncher.IsProcessRunning(processInfo.ProcessId)) continue;
                logger.LogWarning("ScreenshotCapture process {ProcessId} in session {SessionId} is no longer running",
                    processInfo.ProcessId, sessionId);

                _userProcesses.Remove(sessionId);

                // Schedule restart if session is still active
                _ = Task.Run(function: async () => await HandleProcessCrashAsync(sessionId));
            }
        }
    }

    private async Task HandleSessionLogonAsync(SessionInfo sessionInfo)
    {
        if (!ShouldManageSession(sessionInfo))
        {
            logger.LogDebug("Skipping session {SessionId} - not eligible for management", sessionInfo.SessionId);
            return;
        }

        logger.LogInformation("Starting ScreenshotCapture for new session {SessionId}", sessionInfo.SessionId);
        await EnsureScreenshotProcessAsync(sessionInfo.SessionId);
    }

    private async Task HandleSessionLogoffAsync(SessionInfo sessionInfo)
    {
        await Task.Delay(100);
        logger.LogInformation("Cleaning up ScreenshotCapture for ending session {SessionId}", sessionInfo.SessionId);

        lock (_lockObject)
        {
            if (_userProcesses.Remove(sessionInfo.SessionId, out ProcessInfo? processInfo))
            {
                _restartAttempts.Remove(sessionInfo.SessionId);

                _ = Task.Run(function: async () =>
                {
                    try
                    {
                        await processLauncher.TerminateProcessAsync(processInfo.ProcessId);
                        logger.LogInformation("Terminated ScreenshotCapture process {ProcessId} for session {SessionId}",
                            processInfo.ProcessId, sessionInfo.SessionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to terminate process {ProcessId}", processInfo.ProcessId);
                    }
                });
            }
        }
    }

    private async Task HandleProcessCrashAsync(int sessionId)
    {
        try
        {
            // Delay before restart
            if (_config.ProcessRestartDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.ProcessRestartDelaySeconds));
            }

            // Check if session is still active
            if (!sessionManager.IsSessionActive(sessionId))
            {
                logger.LogInformation("Session {SessionId} is no longer active, skipping process restart", sessionId);
                return;
            }

            // Check restart attempts
            lock (_lockObject)
            {
                int attempts = _restartAttempts.GetValueOrDefault(sessionId, 0);
                if (attempts >= _config.MaxRestartAttempts)
                {
                    logger.LogError("Max restart attempts ({MaxAttempts}) reached for session {SessionId}, giving up",
                        _config.MaxRestartAttempts, sessionId);
                    _restartAttempts.Remove(sessionId);
                    return;
                }
                _restartAttempts[sessionId] = attempts + 1;
            }

            logger.LogInformation("Attempting to restart ScreenshotCapture for session {SessionId} (attempt {Attempt}/{MaxAttempts})",
                sessionId, _restartAttempts[sessionId], _config.MaxRestartAttempts);

            await EnsureScreenshotProcessAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling process crash for session {SessionId}", sessionId);
        }
    }

    private async Task EnsureScreenshotProcessAsync(int sessionId)
    {
        await _launchSemaphore.WaitAsync();
        try
        {
            bool shouldLaunch = false;
            
            // Quick check with minimal lock time
            lock (_lockObject)
            {
                if (_userProcesses.TryGetValue(sessionId, out ProcessInfo? existingProcess))
                {
                    // Verify the process is still running
                    if (processLauncher.IsProcessRunning(existingProcess.ProcessId))
                    {
                        logger.LogDebug("ScreenshotCapture process {ProcessId} already running for session {SessionId}, skipping launch",
                            existingProcess.ProcessId, sessionId);
                        return; // Early exit - process already running
                    }
                    else
                    {
                        // Process died, remove from tracking
                        logger.LogWarning("Existing ScreenshotCapture process {ProcessId} for session {SessionId} is no longer running, will create new one",
                            existingProcess.ProcessId, sessionId);
                        _userProcesses.Remove(sessionId);
                        _restartAttempts.Remove(sessionId); // Clean restart attempts too
                    }
                }
                shouldLaunch = true;
            }

            // Launch process outside of lock (only if needed)
            if (shouldLaunch)
            {
                try
                {
                    string executablePath = _config.GetResolvedScreenshotExecutablePath();
                    var arguments = $"user-session --session-id {sessionId}";

                    logger.LogInformation("Launching ScreenshotCapture in session {SessionId}: {Path} {Args}",
                        sessionId, executablePath, arguments);

                    ProcessInfo processInfo = await processLauncher.LaunchInUserSessionAsync(sessionId, executablePath, arguments);

                    // Store result with minimal lock time
                    lock (_lockObject)
                    {
                        _userProcesses[sessionId] = processInfo;
                        _restartAttempts[sessionId] = 0; // Reset attempts on successful start
                    }

                    logger.LogInformation("Successfully launched ScreenshotCapture process {ProcessId} in session {SessionId}",
                        processInfo.ProcessId, sessionId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to launch ScreenshotCapture in session {SessionId}", sessionId);
                }
            }
        }
        finally
        {
            _launchSemaphore.Release();
        }
    }

    private bool ShouldManageSession(SessionInfo sessionInfo)
    {
        // Only manage active, connected sessions with actual users
        return sessionInfo.State == SessionState.Active &&
            !string.IsNullOrEmpty(sessionInfo.UserName) &&
            sessionInfo.SessionId > 0; // Skip Session 0 (system session)
    }

    private async Task CleanupAsync()
    {
        try
        {
            logger.LogInformation("Cleaning up Watchdog Service...");

            // Stop session monitoring
            sessionManager.SessionChanged -= OnSessionChanged;
            await sessionManager.StopMonitoringAsync();

            // Terminate all managed processes
            var processesToTerminate = new List<ProcessInfo>();
            lock (_lockObject)
            {
                processesToTerminate.AddRange(_userProcesses.Values);
                _userProcesses.Clear();
                _restartAttempts.Clear();
            }

            foreach (ProcessInfo processInfo in processesToTerminate)
            {
                try
                {
                    await processLauncher.TerminateProcessAsync(processInfo.ProcessId);
                    logger.LogInformation("Terminated ScreenshotCapture process {ProcessId}", processInfo.ProcessId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to terminate process {ProcessId} during cleanup", processInfo.ProcessId);
                }
            }

            logger.LogInformation("Watchdog Service cleanup completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cleanup");
        }
    }

    public override void Dispose()
    {
        _launchSemaphore?.Dispose();
        base.Dispose();
    }
}

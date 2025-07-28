using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
namespace WinServicesRAG.Core.Process;

/// <summary>
///     Process launcher for user sessions using CreateProcessAsUser API
/// </summary>
public class UserSessionProcessLauncher(ILogger<UserSessionProcessLauncher> logger) : IUserSessionProcessLauncher, IDisposable
{

    // Windows API Constants
    private const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    private const uint CREATE_NEW_CONSOLE = 0x00000010;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    // Token access rights
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_QUERY_SOURCE = 0x0010;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_ADJUST_GROUPS = 0x0040;
    private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
    private const uint TOKEN_ALL_ACCESS = TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY |
        TOKEN_QUERY_SOURCE | TOKEN_ADJUST_PRIVILEGES |
        TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID;

    // Security impersonation levels
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;

    private const uint PROCESS_TERMINATE = 0x0001;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint WAIT_TIMEOUT = 0x00000102;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async Task<ProcessInfo> LaunchInUserSessionAsync(int sessionId, string executablePath, string arguments = "")
    {
        await Task.Delay(100);
        logger.LogDebug("Launching process in session {SessionId}: {Path} {Args}", sessionId, executablePath, arguments);

        var processInfo = new ProcessInfo
        {
            SessionId = sessionId,
            ExecutablePath = executablePath,
            Arguments = arguments,
            StartTime = DateTime.Now,
            IsRunning = false
        };

        try
        {
            IntPtr userToken = IntPtr.Zero;
            IntPtr duplicatedToken = IntPtr.Zero;
            IntPtr environmentBlock = IntPtr.Zero;

            try
            {
                // Get user token for the session
                if (!WTSQueryUserToken(sessionId, out userToken))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to get user token for session {sessionId}. Error: {error}");
                }

                // Duplicate the token
                if (!DuplicateTokenEx(userToken, TOKEN_ALL_ACCESS, IntPtr.Zero,
                    SecurityImpersonation, TokenPrimary, out duplicatedToken))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to duplicate token. Error: {error}");
                }

                // Create environment block for the user
                if (!CreateEnvironmentBlock(out environmentBlock, duplicatedToken, false))
                {
                    logger.LogWarning("Failed to create environment block, using null environment");
                    environmentBlock = IntPtr.Zero;
                }

                // Setup startup info
                var startupInfo = new STARTUPINFO
                {
                    cb = Marshal.SizeOf(typeof(STARTUPINFO)),
                    lpDesktop = "winsta0\\default", // Default desktop
                    dwFlags = STARTF_USESHOWWINDOW,
                    wShowWindow = SW_HIDE // Start hidden by default
                };

                string commandLine = string.IsNullOrEmpty(arguments) ?
                    $"\"{executablePath}\"" :
                    $"\"{executablePath}\" {arguments}";

                string workingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.SystemDirectory;

                // Create the process
                if (CreateProcessAsUser(
                    duplicatedToken,
                    null, // Use command line instead
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE | CREATE_UNICODE_ENVIRONMENT,
                    environmentBlock,
                    workingDirectory,
                    ref startupInfo,
                    out PROCESS_INFORMATION processInformation))
                {
                    processInfo.ProcessId = processInformation.dwProcessId;
                    processInfo.IsRunning = true;

                    logger.LogInformation("Successfully launched process {ProcessId} in session {SessionId}",
                        processInfo.ProcessId, sessionId);

                    // Close handles we don't need
                    CloseHandle(processInformation.hProcess);
                    CloseHandle(processInformation.hThread);
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    var errorMessage = $"Failed to create process. Error: {error}";
                    processInfo.LastError = errorMessage;
                    throw new InvalidOperationException(errorMessage);
                }
            }
            finally
            {
                // Cleanup tokens and environment
                if (userToken != IntPtr.Zero)
                    CloseHandle(userToken);
                if (duplicatedToken != IntPtr.Zero)
                    CloseHandle(duplicatedToken);
                if (environmentBlock != IntPtr.Zero)
                    DestroyEnvironmentBlock(environmentBlock);
            }
        }
        catch (Exception ex)
        {
            processInfo.LastError = ex.Message;
            logger.LogError(ex, "Failed to launch process in session {SessionId}", sessionId);
            throw;
        }

        return processInfo;
    }

    public async Task<bool> TerminateProcessAsync(int processId)
    {
        await Task.Delay(100);
        try
        {
            logger.LogDebug("Terminating process {ProcessId}", processId);

            IntPtr processHandle = OpenProcess(PROCESS_TERMINATE | PROCESS_QUERY_INFORMATION, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                logger.LogWarning("Failed to open process {ProcessId} for termination", processId);
                return false;
            }

            try
            {
                bool result = TerminateProcess(processHandle, 0);
                if (result)
                {
                    // Wait up to 10 seconds for process to terminate
                    uint waitResult = WaitForSingleObject(processHandle, 10000);
                    if (waitResult == WAIT_TIMEOUT)
                    {
                        logger.LogWarning("Process {ProcessId} did not terminate within timeout", processId);
                        return false;
                    }

                    logger.LogInformation("Successfully terminated process {ProcessId}", processId);
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    logger.LogError("Failed to terminate process {ProcessId}. Error: {Error}", processId, error);
                    return false;
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error terminating process {ProcessId}", processId);
            return false;
        }
    }

    public bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process not found
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if process {ProcessId} is running", processId);
            return false;
        }
    }

    public async Task<ProcessInfo?> GetProcessInfoAsync(int processId)
    {
        await Task.Delay(100);
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);

            return new ProcessInfo
            {
                ProcessId = processId,
                SessionId = process.SessionId,
                ExecutablePath = process.MainModule?.FileName ?? string.Empty,
                StartTime = process.StartTime,
                IsRunning = !process.HasExited
            };
        }
        catch (ArgumentException)
        {
            // Process not found
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting process info for {ProcessId}", processId);
            return null;
        }
    }

    public async Task<int> KillProcessesByNameAsync(int sessionId, string executableName)
    {
        var killedCount = 0;

        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(executableName));

            foreach (System.Diagnostics.Process process in processes)
            {
                try
                {
                    if (process.SessionId == sessionId)
                    {
                        bool success = await TerminateProcessAsync(process.Id);
                        if (success)
                        {
                            killedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error killing process {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            logger.LogInformation("Killed {Count} processes named '{Name}' in session {SessionId}",
                killedCount, executableName, sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error killing processes by name '{Name}' in session {SessionId}",
                executableName, sessionId);
        }

        return killedCount;
    }

    // P/Invoke declarations
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int ImpersonationLevel,
        int TokenType,
        out IntPtr phNewToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(int SessionId, out IntPtr phToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    // P/Invoke structures
    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
}

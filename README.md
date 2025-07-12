Of course, here is the detailed plan translated into English in Markdown format.

-----

# Windows Monitoring Software Development Plan

**Project:** Windows Monitoring Agent
**Version:** 1.0
**Date:** 2025-07-12
**Author:** vjcspy (Software Architect)

## Overview

This project aims to build an extremely stable and tamper-resistant Windows monitoring system. The system consists of two main components: **WorkerService**, responsible for executing tasks (like screen capture), and **WatchdogService**, which ensures the WorkerService is always running.

### Technology Stack (Recommended)

* **Language:** C\# (.NET 8+)
* **Framework:** ASP.NET Core Worker Service
* **Libraries:**
   * **TopShelf:** For easily creating and managing Windows Services.
   * **System.Reactive (Rx.NET):** For handling asynchronous data streams and API polling.
   * **HttpClientFactory:** For efficient management of HTTP connections.
   * **Serilog/NLog:** For detailed logging.
   * **P/Invoke (DllImport):** For interacting with Windows APIs.
   * **SharpDX:** For using the DirectX Desktop Duplication API.
   * **Windows SDK Contracts:** For using the Windows Graphics Capture API.

-----

## Phase 1: Planning & Design

- [ ] **Requirement Analysis:** Completed, core requirements have been identified.
- [ ] **System Architecture Design:**
   - [ ] Overall architecture diagram (describing the interaction between Worker, Watchdog, and the API Server).
   - [ ] Clearly define the internal communication protocol between Worker and Watchdog (e.g., Named Pipes, Memory-Mapped Files).
- [ ] **API Client Design:**
   - [ ] Define models (C\# classes) corresponding to the JSON returned from the API.
   - [ ] Build a dedicated `ApiClient` class responsible for the calls:
      - `GetJobAsync(string jobId)`
      - `UploadImageAsync(byte[] imageData, string fileName)`
      - `UpdateJobStatusAsync(string jobId, string status, string imageName, string errorMessage)`
- [ ] **Development Environment Setup:**
   - [ ] Create a Git repository.
   - [ ] Structure the Visual Studio solution with 2 separate projects (WorkerService, WatchdogService) and 1 shared project (Shared/Common).
   - [ ] Set up a basic CI/CD pipeline (e.g., GitHub Actions, Azure DevOps).

-----

## Phase 2: WorkerService Development

This is the main service that implements the business logic.

- [ ] **Step 1: Initialize Windows Service:**

   - [ ] Create a Worker Service project using the .NET template.
   - [ ] Integrate TopShelf to configure the service (service name, description, run-as account - `LocalSystem`).
   - [ ] Configure logging (Serilog/NLog) to write to a file and be viewable in DebugView.

- [ ] **Step 2: Implement the Main Processing Flow with Rx.NET:**

   - [ ] Use `Observable.Interval` to periodically call the API to get jobs.
   - [ ] Filter for jobs where `status == "TAKE_SCREEN_SHOT"`.
   - [ ] Process jobs asynchronously using `SelectMany` and `ObserveOn`.
   - [ ] **Error Handling Flow:**
      1.  **In the Main Observable Stream:** Wrap the processing steps (screenshot, upload, update job) in a child `Observable`.
      2.  **Screenshot Error:** If all 3 screenshot methods fail, catch the exception and return an error object.
      3.  **API Error (Upload/Update):** Use the `Retry(3)` operator on the `HttpClient` call. If it still fails after 3 attempts, log a critical error.
      4.  **`Catch` Operator:** Use `Catch` to handle errors from the child processing stream. When an error occurs, it will switch to another stream to call the API to update the status to `ERROR` along with the `error_message`.
      5.  **`Finally` Operator:** Regardless of success or failure, ensure resource cleanup (e.g., releasing GDI handles, temporary files).

- [ ] **Step 3: Develop the Screenshot Module (Critical Task):**

   - [ ] Create an `IScreenshotProvider` interface with a `byte[] TakeScreenshot()` method.
   - [ ] Implement 3 different solutions for this interface. The processing flow will try Solution 1; if it fails (returns null or throws an exception), it will try Solution 2, and finally Solution 3.

  ### Secure Window Screenshot Solutions

  | Criteria | Solution 1: Windows Graphics Capture API | Solution 2: DirectX Desktop Duplication API | Solution 3: WinAPI (BitBlt + PrintWindow) |
      | :--- | :--- | :--- | :--- |
  | **Description** | A modern Windows 10+ API that allows for safe and efficient recording of a window's or the entire screen's content. | A low-level DirectX API that creates a copy of the desktop on the GPU, allowing direct access to the image buffer. | Uses traditional GDI/User32 functions. `BitBlt` for the entire screen and `PrintWindow` for specific windows. |
  | **Pros** | - **Official & Secure:** Recommended by Microsoft.\<br\>- **Efficient:** Hardware-optimized.\<br\>- **Captures most content:** Including UAC, UWP apps, protected content (except DRM). | - **Extremely High Performance:** Operates at the GPU level.\<br\>- **Captures everything:** Including full-screen games, overlays.\<br\>- **Bypasses user-mode protection layers.** | - **Wide Compatibility:** Works on all Windows versions.\<br\>- **Simpler:** Easier to implement than DirectX.\<br\>- `PrintWindow` can sometimes capture windows that `BitBlt` cannot. |
  | **Cons** | - **Requires Windows 10 (1803+)**.\<br\>- Needs to handle a `DispatcherQueue` if the service lacks a UI thread. | - **Complex:** Requires knowledge of DirectX.\<br\>- **Lots of boilerplate code.**\<br\>- May not work without a GPU or with incompatible drivers. | - **Unreliable:** Fails with UAC, DirectX/OpenGL full-screen, transparent windows, protected content.\<br\>- **`PrintWindow` does not always work.** |
  | **Test Checklist** | - [ ] Capture UAC prompt window.\<br\>- [ ] Capture a UWP app window (like Calculator).\<br\>- [ ] Capture a browser window playing a non-DRM video.\<br\>- [ ] Run on a multi-monitor setup. | - [ ] Capture the screen while a full-screen game is running.\<br\>- [ ] Capture the screen with the mouse cursor.\<br\>- [ ] Check performance (CPU/GPU usage).\<br\>- [ ] Test on different graphics cards (NVIDIA, AMD, Intel). | - [ ] Capture the desktop.\<br\>- [ ] Capture a Notepad window.\<br\>- [ ] Attempt to capture Task Manager (may fail).\<br\>- [ ] Attempt to capture a window with hardware acceleration enabled in Chrome. |
  | **Risks & Prereqs** | **Prerequisites:** Windows 10 build 17134 or newer. Requires a reference to `Windows.SDK.Contracts`.\<br\>**Risk:** Proper initialization and authorization must be handled correctly, even when running as System. | **Prerequisites:** DirectX 11.1+. Requires a library like `SharpDX`.\<br\>**Risk:** Driver errors can cause crashes. Complex GPU resource management. | **Prerequisites:** Any version of Windows.\<br\>**Risk:** Silent failures (returns a black/empty image). GDI handle leaks if not managed carefully. |

- [ ] **Step 4: Finalize API Client and Upload Flow:**

   - [ ] Implement `HttpClientFactory` to create the client.
   - [ ] Build the logic to call the `POST /upload/image` API with `MultipartFormDataContent`.
   - [ ] Build the logic to call the `PATCH /job/{job_id}` API to update the status to `TAKE_SCREEN_SHOT_SUCCESS` or `ERROR`.

-----

## Phase 3: WatchdogService Development

This service has one single goal: to ensure the WorkerService is always running.

- [ ] **Step 1: Initialize Watchdog Service:**
   - [ ] Create a Worker Service project similar to the Worker.
   - [ ] Integrate TopShelf, configure it to run as `LocalSystem`, and give it a distinct service name (e.g., `SystemHealthMonitor`).
- [ ] **Step 2: Implement Monitoring Logic:**
   - [ ] Run an infinite loop (with a reasonable `Task.Delay`, e.g., 5 seconds).
   - [ ] Inside the loop, check for the existence of the `WorkerService.exe` process using `Process.GetProcessesByName()`.
   - [ ] If the process is not found, execute the restart action.
- [ ] **Step 3: Enhance Tamper Resistance (Hardening):**
   - [ ] **Self-Recovery:**
      - [ ] Configure the Windows Service Recovery Options (via TopShelf or the `sc failure` command). Set "Restart the Service" for First, Second, and Subsequent failures. Apply this to both the Worker and the Watchdog.
   - [ ] **Mutual Monitoring:**
      - [ ] **Watchdog monitors Worker:** Logic is already in Step 2.
      - [ ] **Worker monitors Watchdog:** Add a background thread in WorkerService to check for the existence of the `WatchdogService.exe` process. If the Watchdog is killed, the Worker will restart it. This creates a dependency loop, making it harder to shut down both.
   - [ ] **Anti-Tampering:**
      - [ ] On startup, both services check the hash (SHA256) of their own executable file and the other service's file. If the hash does not match a pre-embedded value, the service will raise an alarm (log a critical error) and may refuse to start.
   - [ ] **Anti-Debug:**
      - [ ] Add P/Invoke calls to `CheckRemoteDebuggerPresent()` or `IsDebuggerPresent()` at sensitive points in the code. If a debugger is detected, the service can exit immediately or exhibit deviant behavior.
   - [ ] **Windows Event Log Monitoring:**
      - [ ] The Watchdog can subscribe to events in the `Application Event Log`. If it detects an Error event originating from `WorkerService`, it can immediately trigger the restart process without waiting for the next polling cycle.

-----

## Phase 4: Integration, Testing & Deployment

- [ ] **Integration Testing:**
   - [ ] Test the end-to-end flow: API server returns a job -\> Worker receives it -\> Worker takes a screenshot -\> Worker uploads -\> Worker updates the status.
   - [ ] Test the mutual monitoring: With both services running, kill one and verify that the other restarts it.
- [ ] **Fault Tolerance Testing:**
   - [ ] **Kill Process:** Use Task Manager (with Admin rights) to kill the `WorkerService.exe` process. Measure the time it takes for the Watchdog to restart it. Repeat for `WatchdogService.exe`.
   - [ ] **Stop Service:** Use `services.msc` to Stop the WorkerService. Check if the Watchdog detects this and restarts it (note: this may be harder as the service is stopped "cleanly").
   - [ ] **Simulate Crash:** Add a line of code `throw new Exception("Simulated Crash")` to the WorkerService to see if it restarts according to its Recovery configuration.
   - [ ] **Network Disconnection:** Disconnect the network and verify how the Worker handles API errors (retries, logs, and continues to operate).
- [ ] **Packaging and Deployment:**
   - [ ] Build an installer using WiX Toolset or Inno Setup.
   - [ ] **Installer Tasks:**
      - [ ] Copy the executable files for both services to `C:\Program Files\YourAppName`.
      - [ ] Run the command-line to register both Windows Services (`YourApp.exe install start`).
      - [ ] Configure the Recovery options for both services using the `sc failure` command.
      - [ ] Ensure both services are started after the installation is complete.
- [ ] **Documentation:**
   - [ ] Write an installation guide.
   - [ ] Write documentation describing configuration and how to check logs.
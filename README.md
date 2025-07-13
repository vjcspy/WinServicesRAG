Of course, here is the detailed plan translated into English in Markdown format.

-----

# Windows Monitoring Software Development Plan

**Project:** Windows Monitoring Agent
**Version:** 1.0
**Date:** 2025-07-12
**Author:** vjcspy (Software Architect)

## Overview

This project aims to build an extremely stable and tamper-resistant Windows monitoring system. The system consists of three main components:

1. **WorkerService** - Windows Service responsible for API communication and job orchestration
2. **WatchdogService** - Windows Service that ensures WorkerService is always running  
3. **ScreenshotCapture** - Console Application that handles screenshot capture in user session
4. **WinServicesRAG.Core** - Shared library for other servces like `WorkerService`, `ScreenshotCapture` can refer

### Architecture Overview

```
                                 ┌─────────────────┐
                                 │   API Server    │
                                 │                 │
                                 │ - Get Jobs      │
                                 │ - Upload Images │
                                 │ - Update Status │
                                 │ - Job Queue     │
                                 └─────────────────┘
                                          ▲
                                          │ HTTP API
                              ┌───────────┼───────────┐
                              │           │           │
                              ▼           ▼           ▼
                ┌──────────────────┐              ┌─────────────────┐
                │  WorkerService   │              │ ScreenshotCapture│
                │  (Session 0)     │              │  (User Session) │
                │                  │              │                 │
                │ - API Client     │              │ - API Client    │
                │ - Job Processing │              │ - Screenshot    │
                │ - Business Logic │              │ - DirectX API   │
                │ - Data Storage   │              │ - WinAPI GDI    │
                │ - Service Host   │              │ - CLI Interface │
                └──────────────────┘              └─────────────────┘
                          ▲                                  ▲
                          │                                  │
                          │ Monitor & Restart                │ Monitor & Restart
                          │                                  │
                 ┌────────────────────────────────────────────┐
                 │           WatchdogService                  │
                 │           (Session 0)                      │
                 │                                            │
                 │ ┌─────────────────┐ ┌─────────────────────┐│
                 │ │ Worker Monitor  │ │Screenshot Monitor   ││
                 │ │                 │ │                     ││
                 │ │ - Health Check  │ │ - Process Health    ││
                 │ │ - Auto Restart  │ │ - Session Detection ││
                 │ │ - Log Monitor   │ │ - Auto Launch       ││
                 │ └─────────────────┘ └─────────────────────┘│
                 └────────────────────────────────────────────┘
```

**Key Design Decisions:**

### Why Independent Dual API Architecture?

**Technical Requirement - Complete Service Isolation:**

- **WorkerService (Session 0)**: Handles business logic, data processing, and general API operations but **cannot access desktop graphics**
- **ScreenshotCapture (User Session)**: Specialized for desktop capture using DirectX/Graphics APIs but runs in user context
- **Both services are completely independent**: Each has its own API client, job polling, error handling, and communication with API Server
- **No shared state or communication**: Services operate in parallel without any dependency on each other

**This architecture provides:**
- **Maximum Reliability**: One service failure doesn't affect the other
- **Session Compatibility**: Each service runs in its optimal environment 
- **Scalability**: Services can be scaled independently
- **Simplified Development**: No complex inter-process communication needed

### Architecture Principles:
- **Independent & Simultaneous Operation**: Both WorkerService and ScreenshotCapture work directly with API Server in parallel
- **Session Separation**: WorkerService runs in Session 0 (system), ScreenshotCapture runs in User Session for desktop access
- **No Inter-Service Communication**: WorkerService and ScreenshotCapture do NOT communicate with each other directly
- **Dual API Clients**: Each service has its own HTTP client for independent API communication
- **WatchdogService Management**: WatchdogService monitors and manages both independent processes
- **Isolated Responsibilities**: Each component has distinct, non-overlapping responsibilities
- **High Availability**: Independent operation ensures one service failure doesn't affect the other

### ScreenshotCapture Protection Strategy:
Since ScreenshotCapture cannot run as Windows Service (due to session constraints), it requires special protection:
- **Process Protection**: Run with elevated privileges and system-like characteristics
- **Auto-restart Mechanism**: WatchdogService monitors and restarts if terminated
- **Stealth Operation**: Hidden UI, minimal resource footprint, resistant to casual termination
- **Session Detection**: Automatically detect user logon/logoff and adapt accordingly

### Technology Stack (Updated for Windows 11)

* **Language:** C# (.NET 9)
* **Framework:** ASP.NET Core Worker Service
* **Libraries:**
   * **TopShelf:** For easily creating and managing Windows Services.
   * **System.Reactive (Rx.NET):** For handling asynchronous data streams and API polling.
   * **HttpClientFactory:** For efficient management of HTTP connections.
   * **Serilog/NLog:** For detailed logging.
   * **P/Invoke (DllImport):** For interacting with Windows APIs.
   * **Vortice.Windows:** Modern DirectX wrapper for .NET (replaces obsolete SharpDX).
   * **Microsoft.Extensions.Logging:** For comprehensive logging throughout all services.

-----

## Phase 1: Planning & Design

- [x] **Requirement Analysis:** Completed, core requirements have been identified.
- [x] **System Architecture Design:**
   - [x] Overall architecture diagram (describing the interaction between Worker, Watchdog, and the API Server).
   - [x] Clearly define the internal communication protocol between Worker and Watchdog (Independent dual API architecture - no direct communication).
- [x] **API Client Design:**
   - [x] Define models (C# classes) corresponding to the JSON returned from the API.
   - [x] Build a dedicated `ApiClient` class responsible for the calls:
      - `GetJobAsync(string jobId)`
      - `GetJobsAsync(string status, string? jobType, int limit)`
      - `UploadImageAsync(byte[] imageData, string fileName)`
      - `UpdateJobStatusAsync(string jobId, string status, string imageName, string errorMessage)`
      - `UploadImageAndUpdateJobAsync(string jobId, byte[] imageData, string fileName)`
      - `HealthCheckAsync()`
- [x] **Development Environment Setup:**
   - [x] Create a Git repository.
   - [x] Structure the Visual Studio solution with 3 separate projects (WorkerService, WatchdogService, ScreenshotCapture) and 1 shared project (WinServicesRAG.Core).
   - [ ] Set up a basic CI/CD pipeline (e.g., GitHub Actions, Azure DevOps).

-----

## Phase 2: WorkerService Development

This is the main service that implements the business logic.

- [x] **Step 1: Initialize Windows Service:**

   - [x] Create a Worker Service project using the .NET template.
   - [x] Integrate Serilog for logging configuration (service name, description, run-as account).
   - [x] Configure logging (Serilog) to write to a file and be viewable in DebugView.

- [x] **Step 2: Implement the Main Processing Flow with Rx.NET:**

   - [x] Implement `JobProcessingEngineBase` with `Observable.Interval` to periodically call the API to get jobs.
   - [x] Filter for jobs where `status == "PENDING"` (WorkerService) and `status == "TAKE_SCREEN_SHOT"` (ScreenshotCapture).
   - [x] Process jobs asynchronously using `SelectMany` and `ObserveOn` with Reactive Extensions.
   - [x] **Screenshot Delegation:** WorkerService focuses on API communication and job processing, not screenshot capture.
   - [x] **Job Processing Flow:**
      1. Poll API for jobs with appropriate status filters
      2. Process jobs based on type (SystemInfo, FileOperation, CustomCommand, etc.)
      3. Update job status and handle API communication
      4. Monitor for completed job results via observables
   - [x] **Error Handling Flow:**
      1.  **In the Main Observable Stream:** Wrap the processing steps in child `Observable` with retry logic.
      2.  **Processing Error:** If job processing fails, log error and update job status appropriately.
      3.  **API Error (Upload/Update):** Use the `Retry(3)` operator on the `HttpClient` call. If it still fails after 3 attempts, log a critical error.
      4.  **`Catch` Operator:** Use `Catch` to handle errors from the child processing stream. When an error occurs, it will switch to another stream to call the API to update the status to `ERROR` along with the `error_message`.
      5.  **`Finally` Operator:** Regardless of success or failure, ensure resource cleanup (e.g., temporary files, process handles).

- [x] **Step 3: Develop the Screenshot Module (Critical Task):** ✅ **COMPLETED**

   **Architecture Implementation:** Successfully implemented as an independent parallel service with modern technology stack.

   - [x] Created a separate `ScreenshotCapture` console application project.
   - [x] Implemented continuous monitoring and job processing capabilities.
   - [x] Independent operation parallel with WorkerService - no direct communication required.
   - [x] **Enhanced Screenshot Implementation:**
      - [x] **DirectX Desktop Duplication API (Vortice.Windows)** - Primary provider, optimized for Windows 11
      - [x] **WinAPI (BitBlt + PrintWindow)** - Reliable fallback, works on all Windows versions  
      - [x] **Windows Graphics Capture API Placeholder** - Future implementation when .NET compatibility improves
      - [x] **Fallback Strategy:** Automatic provider selection with graceful degradation
   
   - [x] **Advanced Features Implemented:**
      - [x] CLI mode for testing and debugging (`dotnet run -- cli --help`)
      - [x] Provider status checking (`dotnet run -- cli --status`)
      - [x] Manual provider selection (`dotnet run -- cli --provider "WinAPI"`)
      - [x] Verbose logging support for troubleshooting
      - [x] Comprehensive error handling and retry mechanisms
      - [x] Modern .NET 9 compatibility with Vortice.Windows DirectX wrapper
      - [x] **New:** Async API integration with `IScreenshotManager` interface
      - [x] **New:** Enhanced `ScreenshotResult` model with detailed error information
   
   - [x] **Test Results (Windows 11):**
      - [x] ✅ DirectX Desktop Duplication API: Available in user session, high performance
      - [x] ✅ WinAPI (BitBlt): Available and working, produced 219KB screenshot
      - [x] ⚠️ Windows Graphics Capture API: Disabled due to .NET Runtime compatibility issues
      - [x] ✅ Automatic provider fallback working correctly
      - [x] ✅ CLI testing interface functional and user-friendly

- [x] **Step 4: Finalize API Client and Upload Flow:**

   - [x] Implement `HttpClientFactory` to create the client.
   - [x] Build the logic to call the `POST /upload/image` API with `MultipartFormDataContent`.
   - [x] Build the logic to call the `PATCH /job/{job_id}` API to update the status to `TAKE_SCREEN_SHOT_SUCCESS` or `ERROR`.
   - [x] **New:** Comprehensive retry logic with configurable attempts and delays
   - [x] **New:** Structured error handling with typed responses
   - [x] **New:** Health check endpoint support for monitoring API connectivity

- [x] **Step 5: Core Shared Library Implementation:**

   - [x] **WinServicesRAG.Core Library:** Centralized shared functionality for all services
      - [x] **API Models:** `JobModel`, `UpdateJobStatusRequest`, `ApiResponse<T>`, `JobListResponse`, `ImageUploadResponse`
      - [x] **Configuration:** `ApiClientOptions` with comprehensive settings (retry, timeout, authentication)
      - [x] **Services:** 
         - `IApiClient` and `ApiClient` implementation with retry logic and error handling
         - `IScreenshotManager` and enhanced `ScreenshotManager` with async support
      - [x] **Processing Engines:** 
         - `JobProcessingEngineBase` - Abstract base with Rx.NET implementation
         - `ScreenshotJobProcessingEngine` - Specialized for screenshot tasks
         - `GeneralJobProcessingEngine` - For system info, file operations, custom commands
      - [x] **Dependency Injection:** `ServiceCollectionExtensions` for easy configuration
         - `AddWinServicesRAGCore()` - Core services registration
         - `AddScreenshotServices()` - ScreenshotCapture-specific configuration
         - `AddWorkerServices()` - WorkerService-specific configuration
         - `ValidateConfiguration()` - Configuration validation

- [x] **Step 6: WorkerService Integration:**

   - [x] Updated WorkerService to use new Core library
   - [x] Dependency injection configuration with proper API client setup
   - [x] Observable-based job processing with error handling
   - [x] Configuration files with development and production settings
   - [x] Enhanced logging with structured output and performance monitoring

-----

## Phase 3: WatchdogService Development

This service manages both WorkerService and ScreenshotCapture processes, ensuring high availability and system resilience.

- [ ] **Step 1: Initialize Dual-Process Watchdog Service:**
   - [ ] Create a Worker Service project similar to the Worker.
   - [ ] Integrate TopShelf, configure it to run as `LocalSystem`, and give it a distinct service name (e.g., `SystemHealthMonitor`).
   - [ ] Design monitoring architecture for multiple independent processes.

- [ ] **Step 2: Implement Multi-Process Monitoring Logic:**
   - [ ] **WorkerService Monitoring:**
      - [ ] Run an infinite loop (with a reasonable `Task.Delay`, e.g., 5 seconds).
      - [ ] Check for the existence of the `WorkerService.exe` process using `Process.GetProcessesByName()`.
      - [ ] If the process is not found, execute the restart action.
   - [ ] **ScreenshotCapture Monitoring:**
      - [ ] Monitor ScreenshotCapture process health and session context.
      - [ ] Detect user session changes and restart ScreenshotCapture in appropriate session.
      - [ ] Implement health checks via filesystem communication or API status.
      - [ ] Handle session switching scenarios (logoff/logon, RDP sessions).

- [ ] **Step 3: Enhanced Process Management:**
   - [ ] **Independent Process Lifecycle:**
      - [ ] Start both WorkerService and ScreenshotCapture independently.
      - [ ] Handle process dependencies and startup sequencing.
      - [ ] Implement graceful shutdown coordination.
   - [ ] **Session-Aware Management:**
      - [ ] Detect active user sessions for ScreenshotCapture deployment.
      - [ ] Handle multiple user sessions and session switching.
      - [ ] Implement session 0 isolation awareness.

- [ ] **Step 4: Enhance Tamper Resistance (Hardening):**
   - [ ] **Self-Recovery:**
      - [ ] Configure the Windows Service Recovery Options (via TopShelf or the `sc failure` command). Set "Restart the Service" for First, Second, and Subsequent failures. Apply this to WorkerService, ScreenshotCapture, and the Watchdog.
   - [ ] **Mutual Monitoring:**
      - [ ] **Watchdog monitors both processes:** Logic implemented in Step 2.
      - [ ] **WorkerService monitors Watchdog:** Add a background thread in WorkerService to check for the existence of the `WatchdogService.exe` process. If the Watchdog is killed, the Worker will restart it.
      - [ ] **ScreenshotCapture health reporting:** Periodic health signals to WatchdogService.
   - [ ] **Anti-Tampering:**
      - [ ] On startup, all services check the hash (SHA256) of their own executable file and related service files. If the hash does not match a pre-embedded value, the service will raise an alarm (log a critical error) and may refuse to start.
      - [ ] Include integrity verification for the entire service ecosystem.
   - [ ] **Anti-Debug:**
      - [ ] Add P/Invoke calls to `CheckRemoteDebuggerPresent()` or `IsDebuggerPresent()` at sensitive points in the code. If a debugger is detected, the service can exit immediately or exhibit deviant behavior.
   - [ ] **Windows Event Log Monitoring:**
      - [ ] The Watchdog can subscribe to events in the `Application Event Log`. If it detects an Error event originating from `WorkerService` or `ScreenshotCapture`, it can immediately trigger the restart process without waiting for the next polling cycle.

-----

## Phase 4: Integration, Testing & Deployment

- [ ] **Integration Testing:**
   - [ ] **Independent Dual API Testing:** Test that both WorkerService and ScreenshotCapture operate independently with their own API connections
   - [ ] **Simultaneous Operation Testing:** Both services polling API Server simultaneously without conflicts
   - [ ] **Job Isolation Testing:** Verify WorkerService processes business jobs while ScreenshotCapture handles screenshot requests independently
   - [ ] **API Load Testing:** Test API Server handling multiple simultaneous connections from both services
   - [ ] **WatchdogService Testing:** Test dual-process monitoring - kill one service and verify Watchdog restarts it without affecting the other
   - [ ] **Session Isolation Testing:** Verify that ScreenshotCapture works correctly in user session while WorkerService operates in Session 0
   - [ ] **Independent Failover Testing:** Test various failure scenarios and verify services can recover independently
- [ ] **Fault Tolerance Testing:**
   - [ ] **Kill Process:** Use Task Manager (with Admin rights) to kill the `WorkerService.exe` process. Measure the time it takes for the Watchdog to restart it. Repeat for `WatchdogService.exe`.
   - [ ] **Kill ScreenshotCapture:** Test behavior when ScreenshotCapture process is terminated during operation.
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
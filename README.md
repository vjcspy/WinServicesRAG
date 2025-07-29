Of course, here is the detailed plan translated into English in Markdown format.

-----

# Windows Monitoring Software Development Plan

**Project:** Windows Monitoring Agent
**Version:** 1.0
**Date:** 2025-07-12
**Author:** vjcspy (Software Architect)

## Overview

This project aims to build an extremely stable and tamper-resistant Windows monitoring system. The system consists of three main components:

1. **ScreenshotCapture** - Unified service/console application that handles both screenshot capture and general API operations
2. **WatchdogService** - Windows Service that ensures ScreenshotCapture is always running  
3. **WinServicesRAG.Core** - Shared library for common functionality

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
                                  │
                                  │
                                  │           
                                  ▼           
                ┌──────────────────────────────────────┐
                │       ScreenshotCapture              │
                │    (Service & Console Modes)         │
                │                                      │
                │ Service Mode:                        │
                │ - API Client                         │
                │ - Job Processing                     │
                │ - Screenshot Capture                 │
                │ - Business Logic                     │
                │ - Windows Service Host               │
                │                                      │
                │ CLI Mode:                            │
                │ - Manual Testing                     │
                │ - Provider Status Check              │
                │ - Direct Screenshot Testing          │
                │ - Debugging Interface                │
                │                                      │
                │ Screenshot Technologies:             │
                │ - Windows Graphics Capture API (WGC) │
                │ - DirectX Desktop Duplication API    │
                │ - GDI (Graphics Device Interface)    │
                │ - WinAPI (BitBlt + PrintWindow)      │
                └──────────────────────────────────────┘
                                  ▲
                                  │
                                  │ Monitor & Restart
                                  │
             ┌────────────────────────────────────────────┐
             │           WatchdogService                  │
             │           (Session 0)                      │
             │                                            │
             │ ┌─────────────────────────────────────────┐│
             │ │     ScreenshotCapture Monitor           ││
             │ │                                         ││
             │ │ - Health Check                          ││
             │ │ - Auto Restart                          ││
             │ │ - Log Monitor                           ││
             │ │ - Session Detection                     ││
             │ │ - Service/CLI Mode Management           ││
             │ └─────────────────────────────────────────┘│
             └────────────────────────────────────────────┘
```

**Key Design Decisions:**

### Unified ScreenshotCapture Architecture

**Technical Evolution - Simplified Unified Service:**

- **ScreenshotCapture (Unified Service)**: Now handles all functionality including business logic, data processing, screenshot capture, and API operations
- **Dual Operation Modes**: 
  - **Service Mode**: Runs as Windows Service for production deployment
  - **CLI Mode**: Interactive command-line interface for testing and debugging
- **Session Flexibility**: Can run in both Session 0 (as service) and User Session (for desktop access)
- **Single API Client**: Streamlined architecture with one optimized API client implementation

**This unified architecture provides:**
- **Simplified Deployment**: Single executable handles all functionality
- **Flexible Testing**: CLI mode enables easy debugging and manual testing
- **Reduced Complexity**: No inter-service communication overhead
- **Enhanced Reliability**: Fewer moving parts means fewer failure points
- **Easier Maintenance**: Single codebase for all functionality

### Architecture Principles:
- **Unified Operation**: Single ScreenshotCapture service handles all API communication and processing
- **Dual Modes**: Service mode for production, CLI mode for development and testing
- **Session Adaptability**: Intelligent session detection and appropriate operation mode selection
- **Direct API Integration**: Single optimized HTTP client for all API communication
- **WatchdogService Management**: WatchdogService monitors and manages the unified ScreenshotCapture process
- **Consolidated Responsibilities**: All functionality integrated into one robust, well-tested component
- **High Availability**: Simplified architecture reduces failure points while maintaining reliability

### ScreenshotCapture Protection Strategy:
The unified ScreenshotCapture service includes comprehensive protection:
- **Service Mode Protection**: When running as Windows Service, uses standard Windows Service recovery options
- **Process Protection**: Enhanced privilege management and system-like operational characteristics
- **Auto-restart Mechanism**: WatchdogService monitors and restarts if terminated unexpectedly
- **CLI Mode Safety**: Testing and debugging mode with controlled execution environment
- **Session Detection**: Automatically adapts operation based on session context and requirements
- **Stealth Operation**: Configurable UI visibility, optimized resource usage, robust against casual termination

### Technology Stack (Updated for Windows 11)

* **Language:** C# (.NET 9) + C++ (Native DLL)
* **Framework:** ASP.NET Core Worker Service
* **Libraries:**
   * **TopShelf:** For easily creating and managing Windows Services.
   * **System.Reactive (Rx.NET):** For handling asynchronous data streams and API polling.
   * **HttpClientFactory:** For efficient management of HTTP connections.
   * **Serilog/NLog:** For detailed logging.
   * **P/Invoke (DllImport):** For interacting with Windows APIs and native DLLs.
   * **Vortice.Windows:** Modern DirectX wrapper for .NET (replaces obsolete SharpDX).
   * **ScreenCaptureDLL.dll:** Native C++ DLL implementing Windows Graphics Capture API with clean borders.
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
   - [x] Structure the Visual Studio solution with 2 main projects (ScreenshotCapture, WatchdogService) and 1 shared project (WinServicesRAG.Core).
   - [ ] Set up a basic CI/CD pipeline (e.g., GitHub Actions, Azure DevOps).

-----

## Phase 2: ScreenshotCapture Development (COMPLETED)

This unified service implements all business logic, API communication, and screenshot functionality.

- [x] **Step 1: Initialize Unified Windows Service:**

   - [x] Create a unified service project supporting both Service and CLI modes.
   - [x] Integrate Serilog for comprehensive logging configuration.
   - [x] Configure logging (Serilog) to write to files and be viewable in DebugView.
   - [x] Implement dual-mode operation (Service/CLI) with intelligent mode detection.

- [x] **Step 2: Implement Unified Processing Flow with Rx.NET:**

   - [x] Implement `JobProcessingEngineBase` with `Observable.Interval` for periodic API polling.
   - [x] Filter for jobs where `status == "PENDING"` and `status == "TAKE_SCREEN_SHOT"`.
   - [x] Process all job types asynchronously using `SelectMany` and `ObserveOn` with Reactive Extensions.
   - [x] **Unified Job Processing Flow:**
      1. Poll API for jobs with comprehensive status filtering
      2. Process all job types (SystemInfo, FileOperation, CustomCommand, Screenshot)
      3. Update job status and handle all API communication
      4. Monitor for completed job results via observables
   - [x] **Comprehensive Error Handling Flow:**
      1. **In the Main Observable Stream:** Wrap processing steps in child `Observable` with retry logic.
      2. **Processing Error:** If job processing fails, log error and update job status appropriately.
      3. **API Error (Upload/Update):** Use `Retry(3)` operator on `HttpClient` calls with exponential backoff.
      4. **`Catch` Operator:** Handle errors from child processing streams and update status to `ERROR`.
      5. **`Finally` Operator:** Ensure comprehensive resource cleanup.

- [x] **Step 3: Advanced Screenshot Module Implementation:** ✅ **COMPLETED**

   **Architecture Implementation:** Successfully implemented as a unified service with comprehensive capabilities.

   - [x] **Enhanced Screenshot Implementation:**
      - [x] **Windows Graphics Capture API (WGC)** - Primary provider using native C++ DLL, clean borders, modern Windows 10+ API
      - [x] **DirectX Desktop Duplication API (Vortice.Windows)** - High-performance provider, optimized for Windows 11
      - [x] **GDI (Graphics Device Interface)** - Good compatibility for older Windows versions
      - [x] **WinAPI (BitBlt + PrintWindow)** - Reliable fallback, works on all Windows versions
      - [x] **Fallback Strategy:** Automatic provider selection with graceful degradation
   
   - [x] **Comprehensive Features Implemented:**
      - [x] **Service Mode**: Full Windows Service implementation with job processing
      - [x] **CLI Mode**: Interactive testing and debugging (`dotnet run -- cli --help`)
      - [x] Provider status checking (`dotnet run -- cli --status`)
      - [x] Manual provider selection (`dotnet run -- cli --provider "WGC"`)
      - [x] Verbose logging support for comprehensive troubleshooting
      - [x] Advanced error handling and retry mechanisms
      - [x] Modern .NET 9 compatibility with native C++ DLL integration
      - [x] Async API integration with `IScreenshotManager` interface
      - [x] Enhanced `ScreenshotResult` model with detailed error information
   
   - [x] **Test Results (Windows 11):**
      - [x] ✅ Windows Graphics Capture API (WGC): Available and working, clean border-free screenshots via native DLL
      - [x] ✅ DirectX Desktop Duplication API: Available in user session, high performance
      - [x] ✅ GDI: Available and working for compatibility scenarios
      - [x] ✅ WinAPI (BitBlt): Available and working, produced optimized screenshots
      - [x] ✅ Automatic provider fallback working correctly
      - [x] ✅ CLI testing interface functional and user-friendly
      - [x] ✅ Service mode operational with full job processing

- [x] **Step 4: Comprehensive API Client and Upload Flow:**

   - [x] Implement `HttpClientFactory` for optimized client management.
   - [x] Build logic for `POST /upload/image` API with `MultipartFormDataContent`.
   - [x] Build logic for `PATCH /job/{job_id}` API to update status.
   - [x] **Enhanced Features:**
      - [x] Comprehensive retry logic with configurable attempts and exponential backoff
      - [x] Structured error handling with typed responses
      - [x] Health check endpoint support for monitoring API connectivity
      - [x] Support for all job types (not just screenshots)

- [x] **Step 5: Consolidated Core Library Integration:**

   - [x] **WinServicesRAG.Core Library:** Centralized shared functionality
      - [x] **API Models:** Complete model set for all API operations
      - [x] **Configuration:** `ApiClientOptions` with comprehensive settings
      - [x] **Services:** 
         - `IApiClient` and `ApiClient` with advanced retry and error handling
         - `IScreenshotManager` with async support and multiple providers
      - [x] **Processing Engines:** 
         - `JobProcessingEngineBase` - Abstract base with Rx.NET implementation
         - Unified processing for all job types
      - [x] **Dependency Injection:** Comprehensive service registration
         - `AddWinServicesRAGCore()` - Core services registration
         - `AddScreenshotServices()` - Complete ScreenshotCapture configuration
         - `ValidateConfiguration()` - Configuration validation

- [x] **Step 6: Final Integration:**

   - [x] Unified ScreenshotCapture handles all functionality
   - [x] Comprehensive dependency injection configuration
   - [x] Observable-based job processing with advanced error handling
   - [x] Configuration files for development and production environments
   - [x] Enhanced logging with structured output and performance monitoring

-----

## Phase 3: WatchdogService Development (COMPLETED)

**Implementation Status:** ✅ **Core functionality completed** - WatchdogService successfully monitors ScreenshotCapture processes across user sessions with smart session management and automatic restart capabilities.

This service manages the ScreenshotCapture process, ensuring high availability and system resilience.

- [x] **Step 1: Initialize ScreenshotCapture Watchdog Service:** ✅ **COMPLETED**
   - [x] Create a Worker Service project focused on monitoring ScreenshotCapture.
   - [x] Implement as BackgroundService using Microsoft.Extensions.Hosting.WindowsServices.
   - [x] Configure to run as Windows Service with smart session management.
   - [x] Design monitoring architecture for the unified ScreenshotCapture service.

- [x] **Step 2: Implement ScreenshotCapture Monitoring Logic:** ✅ **COMPLETED**
   - [x] **ScreenshotCapture Service Monitoring:**
      - [x] Run monitoring loop with configurable heartbeat interval (default 10 seconds).
      - [x] Track ScreenshotCapture processes per user session using `ProcessInfo` objects.
      - [x] Monitor process health via `IUserSessionProcessLauncher.IsProcessRunning()`.
      - [x] Automatic restart on process crash with configurable retry attempts.
   - [x] **Enhanced Health Checking:**
      - [x] Monitor ScreenshotCapture process health and operational status.
      - [x] Session change detection via `ISessionManager` with WTS API integration.
      - [x] Handle session switching scenarios (logoff/logon, RDP sessions).
      - [x] Comprehensive logging for troubleshooting and monitoring.

- [x] **Step 3: Enhanced Process Management:** ✅ **COMPLETED**
   - [x] **Unified Process Lifecycle:**
      - [x] Launch ScreenshotCapture in user-session mode with session-specific arguments.
      - [x] Graceful process termination on session logoff.
      - [x] Smart path resolution with automatic ScreenshotCapture.exe detection.
   - [x] **Session-Aware Management:**
      - [x] Active user session detection using Windows Terminal Services APIs.
      - [x] Multi-session support for RDP and multiple user scenarios.
      - [x] Session 0 isolation awareness - WatchdogService runs in Session 0, manages user session processes.
      - [x] Configurable session filtering (skip system sessions, inactive sessions).

- [ ] **Step 4: Enhance Tamper Resistance (Hardening):** ⚠️ **PARTIALLY COMPLETED**
   - [x] **Self-Recovery:**
      - [x] Implemented Windows Service with automatic restart capabilities.
      - [x] Configurable restart attempts with exponential backoff.
      - [x] Process crash detection and recovery with session awareness.
   - [ ] **Mutual Monitoring:**
      - [x] **Watchdog monitors ScreenshotCapture:** ✅ Fully implemented with session-aware monitoring.
      - [ ] **ScreenshotCapture monitors Watchdog:** Not yet implemented - add background thread in ScreenshotCapture to check for WatchdogService.exe process.
      - [ ] **Health reporting:** Basic health monitoring implemented, advanced IPC health signals pending.
   - [ ] **Anti-Tampering:**
      - [ ] File hash verification (SHA256) for executable integrity checking.
      - [ ] Include integrity verification for the entire service ecosystem.
   - [ ] **Anti-Debug:**
      - [ ] Add P/Invoke calls to `CheckRemoteDebuggerPresent()` or `IsDebuggerPresent()`.
   - [ ] **Windows Event Log Monitoring:**
      - [ ] Subscribe to Application Event Log for ScreenshotCapture error events.

**Additional Features Implemented:**
- [x] **Smart Path Resolution:** Auto-detection of ScreenshotCapture.exe in various deployment scenarios
- [x] **Configuration Validation:** Comprehensive startup validation with detailed error reporting  
- [x] **Structured Logging:** Serilog integration with file, console, and EventLog outputs
- [x] **Installation Scripts:** PowerShell scripts for service installation/uninstallation
- [x] **Production Deployment:** Published binaries with all dependencies included
- [x] **Core Library Integration:** Full utilization of WinServicesRAG.Core components
- [x] **Interactive Service Pattern:** Successfully implemented session-aware process management

-----

## Phase 4: Integration, Testing & Deployment

- [ ] **Integration Testing:**
   - [ ] **Unified Service Testing:** Test that ScreenshotCapture operates correctly in both Service and CLI modes
   - [ ] **API Integration Testing:** Verify ScreenshotCapture handles all job types with proper API communication
   - [ ] **Job Processing Testing:** Test comprehensive job processing including business logic and screenshot capture
   - [ ] **API Load Testing:** Test API Server handling concurrent requests from ScreenshotCapture
   - [x] **WatchdogService Basic Testing:** ✅ Basic functionality verified - service starts, monitors sessions, manages processes
   - [ ] **Advanced WatchdogService Testing:** Test ScreenshotCapture monitoring - kill the service and verify Watchdog restarts it correctly
   - [ ] **Session Management Testing:** Verify that ScreenshotCapture adapts correctly to different session contexts
   - [ ] **Mode Switching Testing:** Test transitions between Service and CLI modes
   - [ ] **Comprehensive Failover Testing:** Test various failure scenarios and verify service can recover appropriately
- [ ] **Fault Tolerance Testing:**
   - [ ] **Kill Process:** Use Task Manager (with Admin rights) to kill the `ScreenshotCapture.exe` process. Measure the time it takes for the Watchdog to restart it. Repeat for `WatchdogService.exe`.
   - [ ] **Kill ScreenshotCapture:** Test behavior when ScreenshotCapture process is terminated during different operational modes.
   - [ ] **Stop Service:** Use `services.msc` to Stop the ScreenshotCapture service. Check if the Watchdog detects this and restarts it appropriately.
   - [ ] **Simulate Crash:** Add a line of code `throw new Exception("Simulated Crash")` to ScreenshotCapture to test recovery according to its configuration.
   - [ ] **Network Disconnection:** Disconnect the network and verify how ScreenshotCapture handles API errors (retries, logs, and continues to operate).
   - [ ] **Mode Transition Testing:** Test switching between Service and CLI modes under various conditions.
- [ ] **Packaging and Deployment:**
   - [ ] Build an installer using WiX Toolset or Inno Setup.
   - [ ] **Installer Tasks:**
      - [ ] Copy the executable files for ScreenshotCapture and WatchdogService to `C:\Program Files\YourAppName`.
      - [ ] Run the command-line to register both Windows Services.
      - [ ] Configure the Recovery options for both services using the `sc failure` command.
      - [ ] Ensure both services are started after the installation is complete.
- [ ] **Documentation:**
   - [ ] Write an installation guide.
   - [ ] Write documentation describing configuration and how to check logs.
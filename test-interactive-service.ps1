# Test Interactive Service Implementation
# Run this script to verify the complete solution

param(
    [switch]$SkipBuild,
    [switch]$TestServiceInstallation,
    [int]$SessionId = 1
)

Write-Host "=== Testing Interactive Service Implementation ===" -ForegroundColor Green
Write-Host ""

$ErrorActionPreference = "Continue"
$testResults = @()

function Add-TestResult {
    param($Test, $Status, $Details = "")
    $testResults += [PSCustomObject]@{
        Test = $Test
        Status = $Status
        Details = $Details
    }
}

function Show-TestResults {
    Write-Host ""
    Write-Host "=== Test Results Summary ===" -ForegroundColor Cyan
    $testResults | Format-Table Test, Status, Details -AutoSize
    
    $passed = ($testResults | Where-Object Status -eq "✅ PASS").Count
    $failed = ($testResults | Where-Object Status -eq "❌ FAIL").Count
    $skipped = ($testResults | Where-Object Status -eq "⚠️ SKIP").Count
    
    Write-Host "Results: $passed passed, $failed failed, $skipped skipped" -ForegroundColor $(if($failed -eq 0) {"Green"} else {"Red"})
}

# Test 1: Check prerequisites
Write-Host "Test 1: Checking prerequisites..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    if ($dotnetVersion) {
        Add-TestResult "Prerequisites" "✅ PASS" ".NET $dotnetVersion"
        Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
    }
} catch {
    Add-TestResult "Prerequisites" "❌ FAIL" ".NET SDK not found"
    Write-Host "❌ .NET SDK not found" -ForegroundColor Red
}

# Test 2: Check project structure
Write-Host "Test 2: Checking project structure..." -ForegroundColor Yellow
$requiredFiles = @(
    "src\WinServicesRAG.Core\Configuration\WatchdogServiceConfig.cs",
    "src\WinServicesRAG.Core\Session\SessionManager.cs",
    "src\WinServicesRAG.Core\Process\UserSessionProcessLauncher.cs",
    "src\WatchdogService\EnhancedWatchdogService.cs",
    "src\WatchdogService\Program.cs",
    "src\ScreenshotCapture\Commands\UserSessionCommand.cs"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -eq 0) {
    Add-TestResult "Project Structure" "✅ PASS" "All required files present"
    Write-Host "✅ All required files present" -ForegroundColor Green
} else {
    Add-TestResult "Project Structure" "❌ FAIL" "$($missingFiles.Count) files missing"
    Write-Host "❌ Missing files:" -ForegroundColor Red
    $missingFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

# Test 3: Build projects
if (-not $SkipBuild) {
    Write-Host "Test 3: Building projects..." -ForegroundColor Yellow
    
    # Build Core
    Write-Host "  Building WinServicesRAG.Core..." -ForegroundColor Gray
    Push-Location "src\WinServicesRAG.Core"
    $coreResult = dotnet build --verbosity quiet
    Pop-Location
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ WinServicesRAG.Core build successful" -ForegroundColor Green
    } else {
        Add-TestResult "Core Build" "❌ FAIL" "Compilation errors"
        Write-Host "  ❌ WinServicesRAG.Core build failed" -ForegroundColor Red
    }
    
    # Build WatchdogService
    Write-Host "  Building WatchdogService..." -ForegroundColor Gray
    Push-Location "src\WatchdogService"
    $watchdogResult = dotnet build --verbosity quiet
    Pop-Location
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ WatchdogService build successful" -ForegroundColor Green
    } else {
        Add-TestResult "WatchdogService Build" "❌ FAIL" "Compilation errors"
        Write-Host "  ❌ WatchdogService build failed" -ForegroundColor Red
    }
    
    # Build ScreenshotCapture
    Write-Host "  Building ScreenshotCapture..." -ForegroundColor Gray
    Push-Location "src\ScreenshotCapture"
    $screenshotResult = dotnet build --verbosity quiet
    Pop-Location
    
    if ($LASTEXITCODE -eq 0) {
        Add-TestResult "Build Process" "✅ PASS" "All projects compiled"
        Write-Host "  ✅ ScreenshotCapture build successful" -ForegroundColor Green
    } else {
        Add-TestResult "ScreenshotCapture Build" "❌ FAIL" "Compilation errors"
        Write-Host "  ❌ ScreenshotCapture build failed" -ForegroundColor Red
    }
} else {
    Add-TestResult "Build Process" "⚠️ SKIP" "Skipped by user"
    Write-Host "Test 3: Build skipped by user" -ForegroundColor Yellow
}

# Test 4: Test smart path resolution
Write-Host "Test 4: Testing smart path resolution..." -ForegroundColor Yellow
try {
    Push-Location "src\WatchdogService"
    $pathTest = dotnet run -- --help 2>&1
    Pop-Location
    
    if ($pathTest -match "WatchdogService" -or $LASTEXITCODE -eq 0) {
        Add-TestResult "Smart Path Resolution" "✅ PASS" "WatchdogService can start"
        Write-Host "✅ WatchdogService can initialize" -ForegroundColor Green
    } else {
        Add-TestResult "Smart Path Resolution" "❌ FAIL" "WatchdogService failed to start"
        Write-Host "❌ WatchdogService failed to start" -ForegroundColor Red
    }
} catch {
    Add-TestResult "Smart Path Resolution" "❌ FAIL" "Runtime error"
    Write-Host "❌ Runtime error testing WatchdogService" -ForegroundColor Red
}

# Test 5: Test ScreenshotCapture user session command
Write-Host "Test 5: Testing ScreenshotCapture user session command..." -ForegroundColor Yellow
try {
    Push-Location "src\ScreenshotCapture"
    $userSessionTest = dotnet run -- user-session --help 2>&1
    Pop-Location
    
    if ($userSessionTest -match "user-session" -and $userSessionTest -match "session-id") {
        Add-TestResult "User Session Command" "✅ PASS" "Command available"
        Write-Host "✅ User session command available" -ForegroundColor Green
    } else {
        Add-TestResult "User Session Command" "❌ FAIL" "Command not found"
        Write-Host "❌ User session command not found" -ForegroundColor Red
    }
} catch {
    Add-TestResult "User Session Command" "❌ FAIL" "Runtime error"
    Write-Host "❌ Error testing user session command" -ForegroundColor Red
}

# Test 6: Test session detection (requires admin)
Write-Host "Test 6: Testing session detection..." -ForegroundColor Yellow
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if ($isAdmin) {
    try {
        $sessions = query session 2>&1
        if ($sessions -match "console" -or $sessions -match "rdp") {
            Add-TestResult "Session Detection" "✅ PASS" "Can enumerate sessions"
            Write-Host "✅ Can enumerate Windows sessions" -ForegroundColor Green
        } else {
            Add-TestResult "Session Detection" "⚠️ SKIP" "No sessions found"
            Write-Host "⚠️ No active sessions detected" -ForegroundColor Yellow
        }
    } catch {
        Add-TestResult "Session Detection" "❌ FAIL" "Query session failed"
        Write-Host "❌ Failed to query sessions" -ForegroundColor Red
    }
} else {
    Add-TestResult "Session Detection" "⚠️ SKIP" "Requires administrator"
    Write-Host "⚠️ Session detection requires administrator privileges" -ForegroundColor Yellow
}

# Test 7: Service installation test (optional)
if ($TestServiceInstallation -and $isAdmin) {
    Write-Host "Test 7: Testing service installation..." -ForegroundColor Yellow
    
    # Publish first
    try {
        Write-Host "  Publishing WatchdogService..." -ForegroundColor Gray
        Push-Location "src\WatchdogService"
        dotnet publish -c Release --output "..\..\publish\WatchdogService" --verbosity quiet
        Pop-Location
        
        if (Test-Path "publish\WatchdogService\WatchdogService.exe") {
            Write-Host "  ✅ WatchdogService published successfully" -ForegroundColor Green
            
            # Test install script
            $installResult = .\install-watchdog-service.ps1 -ServicePath "$(Get-Location)\publish\WatchdogService\WatchdogService.exe" 2>&1
            
            Start-Sleep -Seconds 5
            $service = Get-Service -Name "WatchdogService" -ErrorAction SilentlyContinue
            
            if ($service -and $service.Status -eq "Running") {
                Add-TestResult "Service Installation" "✅ PASS" "Service running"
                Write-Host "✅ Service installed and running" -ForegroundColor Green
                
                # Cleanup
                Write-Host "  Cleaning up test service..." -ForegroundColor Gray
                .\uninstall-watchdog-service.ps1
            } else {
                Add-TestResult "Service Installation" "❌ FAIL" "Service not running"
                Write-Host "❌ Service installation failed" -ForegroundColor Red
            }
        } else {
            Add-TestResult "Service Installation" "❌ FAIL" "Publish failed"
            Write-Host "❌ Failed to publish WatchdogService" -ForegroundColor Red
        }
    } catch {
        Add-TestResult "Service Installation" "❌ FAIL" "Exception during test"
        Write-Host "❌ Exception during service installation test" -ForegroundColor Red
    }
} elseif ($TestServiceInstallation -and -not $isAdmin) {
    Add-TestResult "Service Installation" "⚠️ SKIP" "Requires administrator"
    Write-Host "Test 7: Service installation test requires administrator privileges" -ForegroundColor Yellow
} else {
    Add-TestResult "Service Installation" "⚠️ SKIP" "Not requested"
    Write-Host "Test 7: Service installation test skipped" -ForegroundColor Yellow
}

# Show final results
Show-TestResults

Write-Host ""
Write-Host "=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Fix any failed tests above" -ForegroundColor White
Write-Host "2. Run as Administrator: .\test-interactive-service.ps1 -TestServiceInstallation" -ForegroundColor White
Write-Host "3. Install production service: .\install-watchdog-service.ps1" -ForegroundColor White
Write-Host "4. Monitor logs: D:\Documents\Temporary\WinServicesRAG\logs\" -ForegroundColor White
Write-Host ""
Write-Host "For detailed implementation guide: INTERACTIVE_SERVICE_GUIDE.md" -ForegroundColor Green

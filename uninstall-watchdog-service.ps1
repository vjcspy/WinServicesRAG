# Uninstall WatchdogService Windows Service
# Run this script as Administrator

param(
    [string]$ServiceName = "WatchdogService"
)

Write-Host "=== Uninstalling Windows System Monitoring Watchdog Service ===" -ForegroundColor Red
Write-Host "Service Name: $ServiceName" -ForegroundColor Yellow

# Check if service exists
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Host "Service '$ServiceName' not found. Nothing to uninstall." -ForegroundColor Yellow
    exit 0
}

Write-Host "Found service: $($service.DisplayName)" -ForegroundColor Yellow
Write-Host "Current status: $($service.Status)" -ForegroundColor Yellow

# Stop the service if running
if ($service.Status -eq "Running") {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force
    
    # Wait for service to stop
    $timeout = 30
    $elapsed = 0
    while ((Get-Service -Name $ServiceName).Status -eq "Running" -and $elapsed -lt $timeout) {
        Start-Sleep -Seconds 1
        $elapsed++
        Write-Host "Waiting for service to stop... ($elapsed/$timeout)" -ForegroundColor Yellow
    }
    
    $finalStatus = (Get-Service -Name $ServiceName).Status
    if ($finalStatus -eq "Stopped") {
        Write-Host "✅ Service stopped successfully" -ForegroundColor Green
    } else {
        Write-Warning "Service may not have stopped cleanly (Status: $finalStatus)"
    }
}

# Delete the service
Write-Host "Removing service registration..." -ForegroundColor Yellow
$result = sc.exe delete $ServiceName

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Service uninstalled successfully" -ForegroundColor Green
    
    # Verify removal
    Start-Sleep -Seconds 2
    $checkService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $checkService) {
        Write-Host "✅ Service registration removed from system" -ForegroundColor Green
    } else {
        Write-Warning "Service may still be registered. Try restarting Windows."
    }
} else {
    Write-Error "Failed to uninstall service"
    Write-Host "Error details: $result" -ForegroundColor Red
}

Write-Host ""
Write-Host "Cleanup suggestions:" -ForegroundColor Cyan
Write-Host "1. Service logs will remain in: D:\Documents\Temporary\WinServicesRAG\logs\" -ForegroundColor White
Write-Host "2. Configuration files remain in project directory" -ForegroundColor White
Write-Host "3. Published binaries remain in: publish\WatchdogService\" -ForegroundColor White
Write-Host ""
Write-Host "To reinstall: .\install-watchdog-service.ps1" -ForegroundColor Cyan

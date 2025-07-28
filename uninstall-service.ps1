# Script to uninstall ScreenshotCapture Windows Service
# Run with Administrator privileges

$ServiceName = "ScreenshotCaptureService"

Write-Host "=== Uninstalling Windows Service ===" -ForegroundColor Red

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    
    Write-Host "Deleting service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    
    Write-Host "Service successfully removed!" -ForegroundColor Green
} else {
    Write-Host "Service does not exist." -ForegroundColor Yellow
}

Write-Host "=== Complete! ===" -ForegroundColor Green

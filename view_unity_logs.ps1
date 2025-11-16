# Unity Log Viewer Script for Android
# Usage: .\view_unity_logs.ps1 [filter]

param(
    [string]$Filter = "Unity"
)

# Try to find ADB
$adbPaths = @(
    "C:\Program Files\Unity\Hub\Editor\6000.0.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe",
    "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
    "$env:USERPROFILE\AppData\Local\Android\Sdk\platform-tools\adb.exe"
)

$adb = $null
foreach ($path in $adbPaths) {
    if (Test-Path $path) {
        $adb = $path
        Write-Host "Found ADB at: $adb" -ForegroundColor Green
        break
    }
}

if (-not $adb) {
    Write-Host "ADB not found! Please install Android SDK or ensure Unity Android SDK is installed." -ForegroundColor Red
    Write-Host "You can also add ADB to your PATH manually." -ForegroundColor Yellow
    exit 1
}

# Check if device is connected
$devices = & $adb devices
if ($devices.Count -lt 2) {
    Write-Host "No Android device connected!" -ForegroundColor Red
    Write-Host "Make sure your Quest/Android device is:" -ForegroundColor Yellow
    Write-Host "  1. Connected via USB (with USB debugging enabled)" -ForegroundColor Yellow
    Write-Host "  2. Or connected via WiFi ADB" -ForegroundColor Yellow
    exit 1
}

Write-Host "`nViewing Unity logs (Press Ctrl+C to stop)..." -ForegroundColor Cyan
Write-Host "Filter: $Filter`n" -ForegroundColor Yellow

# Clear log buffer and start viewing
& $adb logcat -c
& $adb logcat -v time "$Filter`:V" "*:S"


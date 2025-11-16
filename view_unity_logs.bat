@echo off
REM Unity Log Viewer for Android
REM Usage: view_unity_logs.bat

REM Try Unity's ADB first
set ADB_PATH=C:\Program Files\Unity\Hub\Editor\6000.0.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe

if not exist "%ADB_PATH%" (
    REM Try Android SDK
    set ADB_PATH=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe
)

if not exist "%ADB_PATH%" (
    echo ADB not found! Please install Android SDK or ensure Unity Android SDK is installed.
    pause
    exit /b 1
)

echo Found ADB at: %ADB_PATH%
echo.
echo Viewing Unity logs (Press Ctrl+C to stop)...
echo.

REM Clear log buffer
"%ADB_PATH%" logcat -c

REM View Unity logs with timestamps
"%ADB_PATH%" logcat -v time Unity:V FlexGloveBLE:V FlexGloveBridge:V *:S


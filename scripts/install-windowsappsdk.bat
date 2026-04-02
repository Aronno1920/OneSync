@echo off
REM Windows App SDK Runtime Installer
REM This script will download and install the Windows App SDK runtime

echo ========================================
echo Windows App SDK Runtime Installer
echo ========================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Please right-click the script and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

REM Check if Windows App SDK is already installed
echo Checking for Windows App SDK runtime...
if exist "%SystemRoot%\System32\Microsoft.WindowsAppRuntime.1.4.dll" (
    echo Windows App SDK 1.4 is already installed!
    echo.
    goto :verify_installation
)

if exist "%SystemRoot%\System32\Microsoft.WindowsAppRuntime.dll" (
    echo Windows App SDK is already installed!
    echo.
    goto :verify_installation
)

echo Windows App SDK runtime not found.
echo.

REM Download Windows App SDK runtime
set DOWNLOAD_URL=https://aka.ms/windowsappsdk/1.4/1.4.231008000/windowsappruntimeinstall-x64.exe
set OUTPUT_PATH=%TEMP%\windowsappruntimeinstall-x64.exe

echo Download URL: %DOWNLOAD_URL%
echo Output Path: %OUTPUT_PATH%
echo.

echo Downloading Windows App SDK runtime...
powershell -Command "& {Invoke-WebRequest -Uri '%DOWNLOAD_URL%' -OutFile '%OUTPUT_PATH%' -UseBasicParsing}"

if not exist "%OUTPUT_PATH%" (
    echo ERROR: Failed to download Windows App SDK runtime!
    echo.
    echo Please download manually from:
    echo %DOWNLOAD_URL%
    echo.
    pause
    exit /b 1
)

echo Download completed: %OUTPUT_PATH%
echo.

REM Install Windows App SDK runtime
echo Installing Windows App SDK runtime...
echo A UAC prompt will appear. Please click "Yes" to continue.
echo.

start /wait "" "%OUTPUT_PATH%"

REM Clean up
if exist "%OUTPUT_PATH%" (
    del "%OUTPUT_PATH%"
)

echo.
echo Installation completed!
echo.

:verify_installation
echo Verifying installation...
if exist "%SystemRoot%\System32\Microsoft.WindowsAppRuntime.1.4.dll" (
    echo SUCCESS: Windows App SDK 1.4 is installed!
    echo.
    goto :next_steps
)

if exist "%SystemRoot%\System32\Microsoft.WindowsAppRuntime.dll" (
    echo SUCCESS: Windows App SDK is installed!
    echo.
    goto :next_steps
)

echo WARNING: Could not verify installation.
echo Please check if the installation completed successfully.
echo.

:next_steps
echo ========================================
echo Next Steps
echo ========================================
echo.
echo IMPORTANT: Please restart your computer to complete the installation!
echo.
echo After restarting, rebuild your project:
echo   dotnet clean SyncUI/SyncUI.csproj
echo   dotnet restore SyncUI/SyncUI.csproj
echo   dotnet build SyncUI/SyncUI.csproj
echo.
echo Then run your application:
echo   dotnet run --project SyncUI/SyncUI.csproj
echo.

pause

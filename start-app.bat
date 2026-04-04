@echo off
REM OneSync Startup Script
REM This script starts the sync-engine server and the SyncUI application

echo ========================================
echo   OneSync Application Launcher
echo ========================================
echo.

REM Check if we're in the correct directory
if not exist "sync-engine\Cargo.toml" (
    echo ERROR: Please run this script from the OneSync project root directory
    echo Current directory: %CD%
    pause
    exit /b 1
)

echo [1/3] Starting sync-engine server...
cd sync-engine

REM Start the sync-engine server in a new window
start "OneSync Engine" cmd /k "echo Starting OneSync Engine... && cargo run -- --addr 127.0.0.1:50051"

REM Wait a bit for the server to start
echo Waiting for server to initialize...
timeout /t 5 /nobreak >nul

cd ..

echo [2/3] Verifying server is running...
REM Simple check - try to connect to the port (this is a basic check)
powershell -Command "try { $tcp = New-Object System.Net.Sockets.TcpClient; $tcp.Connect('127.0.0.1', 50051); $tcp.Close(); Write-Host 'Server is running on port 50051' -ForegroundColor Green } catch { Write-Host 'Server may not be ready yet, continuing...' -ForegroundColor Yellow }"

echo [3/3] Starting SyncUI application...
cd SyncUI

REM Start the SyncUI application
dotnet run

cd ..

echo.
echo ========================================
echo   OneSync Application Closed
echo ========================================
echo.
echo Note: The sync-engine server window may still be open.
echo You can close it manually if needed.
pause

# COMException Quick Fix Reference

## Problem
MAUI application throws `System.Runtime.InteropServices.COMException` (HResult: 0x80004005) during initialization.

## Root Cause
**Windows App SDK Runtime is not installed** on your system. MAUI Windows applications require this runtime to be installed separately from .NET.

## Quick Fix (3 Steps)

### Step 1: Run Diagnostic Script (Recommended)
```powershell
# Open PowerShell as Administrator
cd f:\Project\OneSync
.\scripts\diagnose-maui-comexception.ps1
```

The script will:
- Check if Windows App SDK runtime is installed
- Offer to install it automatically
- Provide clear instructions if manual installation is needed

### Step 2: Alternative - Manual Installation
If the script doesn't work, install manually:

1. **Download Windows App SDK Runtime:**
   - URL: https://aka.ms/windowsappsdk/1.4/1.4.231008000/windowsappruntimeinstall-x64.exe
   - Version: 1.4.231008000

2. **Install the Runtime:**
   - Run the downloaded installer
   - Follow the prompts
   - Restart your computer if prompted

3. **Verify Installation:**
   ```powershell
   Get-AppxPackage -Name "Microsoft.WindowsAppRuntime*"
   ```

### Step 3: Rebuild and Run
```bash
# Clean and rebuild
dotnet clean SyncUI/SyncUI.csproj
dotnet restore SyncUI/SyncUI.csproj
dotnet build SyncUI/SyncUI.csproj

# Run the application
dotnet run --project SyncUI/SyncUI.csproj
```

## What Was Fixed in Code

### 1. MauiProgram.cs
- Added `CheckWindowsAppSDKRuntime()` method to detect missing runtime
- Enhanced error logging to capture COMException details
- Writes error logs to `AppDataDirectory/maui_error.log`

### 2. App.xaml
- Simplified to defer resource loading
- Removed merged resource dictionaries from XAML

### 3. App.xaml.cs
- Added `LoadResources()` method
- Loads resources programmatically in C# code
- Bypasses COM interface that was causing the exception

## Verification

### Build Should Succeed:
```bash
dotnet build SyncUI/SyncUI.csproj
```
**Expected:** `Build succeeded. 0 Error(s), 47 Warning(s)`

### Runtime Should Work:
After installing Windows App SDK runtime:
```bash
dotnet run --project SyncUI/SyncUI.csproj
```
**Expected:** Application launches without COMException

## Troubleshooting

### Issue: Still Getting COMException

**Solution 1: Clear MAUI Cache**
```bash
dotnet clean SyncUI/SyncUI.csproj
dotnet restore SyncUI/SyncUI.csproj
dotnet build SyncUI/SyncUI.csproj
```

**Solution 2: Restart Computer**
After installing Windows App SDK runtime, restart your computer.

**Solution 3: Check for Multiple Runtime Versions**
```powershell
Get-AppxPackage -Name "Microsoft.WindowsAppRuntime*"
```
Uninstall all versions and reinstall only 1.4.231008000.

### Issue: Build Succeeds but Runtime Crashes

If you see `Access Violation (0xC0000005)`, the Windows App SDK runtime is not properly installed. Reinstall the runtime.

## System Requirements

- **OS:** Windows 10 version 1809 (Build 17763) or later
- **.NET:** .NET 8.0 Runtime
- **Visual C++ Redistributable:** Latest version
- **Windows App SDK Runtime:** Version 1.4.231008000

## Additional Resources

- **Comprehensive Guide:** [`docs/COMEXCEPTION_FIX_GUIDE.md`](docs/COMEXCEPTION_FIX_GUIDE.md)
- **Status Document:** [`docs/COMEXCEPTION_FIX_STATUS.md`](docs/COMEXCEPTION_FIX_STATUS.md)
- **Diagnostic Script:** [`scripts/diagnose-maui-comexception.ps1`](scripts/diagnose-maui-comexception.ps1)

## Key Points

✅ **Build Error:** Fixed with code changes. Application builds successfully.

⚠️ **Runtime Error:** Requires Windows App SDK runtime installation. This is a system dependency, not a code issue.

💡 **Best Practice:** Run the diagnostic script first - it will automatically detect and fix the issue.

## Summary

The COMException is caused by a missing system dependency (Windows App SDK Runtime), not a code bug. The code changes provide better error detection and logging, but the runtime must be installed on your system for the application to work.

**Run the diagnostic script to automatically detect and fix the issue!**

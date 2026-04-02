# MAUI COMException Diagnostic Script
# Run this script as Administrator to diagnose and fix Windows App SDK runtime issues

#Requires -RunAsAdministrator

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MAUI COMException Diagnostic Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to check if running as Administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Check if running as Administrator
if (-not (Test-Administrator)) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click the script and select 'Run as Administrator'" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Function to check Windows App SDK runtime
function Test-WindowsAppSDK {
    Write-Host "Checking for Windows App SDK runtime..." -ForegroundColor Yellow
    
    $runtimeFound = $false
    $runtimeVersion = ""
    
    # Check for Windows App SDK packages
    try {
        $packages = Get-AppxPackage -Name "Microsoft.WindowsAppRuntime*" -ErrorAction SilentlyContinue
        if ($packages) {
            foreach ($package in $packages) {
                Write-Host "  Found Windows App SDK package:" -ForegroundColor Green
                Write-Host "    Name: $($package.Name)" -ForegroundColor Gray
                Write-Host "    Version: $($package.Version)" -ForegroundColor Gray
                $runtimeFound = $true
                $runtimeVersion = $package.Version
            }
        }
    }
    catch {
        Write-Host "  Warning: Could not check Appx packages: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    # Check for Windows App SDK DLLs
    $dllPaths = @(
        "$env:SystemRoot\System32\Microsoft.WindowsAppRuntime.1.4.dll",
        "$env:SystemRoot\System32\Microsoft.WindowsAppRuntime.dll",
        "$env:SystemRoot\SysWOW64\Microsoft.WindowsAppRuntime.1.4.dll",
        "$env:SystemRoot\SysWOW64\Microsoft.WindowsAppRuntime.dll"
    )
    
    $dllFound = $false
    foreach ($dllPath in $dllPaths) {
        if (Test-Path $dllPath) {
            Write-Host "  Found Windows App SDK DLL: $dllPath" -ForegroundColor Green
            $dllFound = $true
        }
    }
    
    return $runtimeFound -or $dllFound
}

# Function to check .NET 8.0 runtime
function Test-DotNetRuntime {
    Write-Host "Checking for .NET 8.0 runtime..." -ForegroundColor Yellow
    
    try {
        $dotnetInfo = dotnet --list-runtimes 2>&1 | Select-String "Microsoft\.NETCore\.App 8\."
        if ($dotnetInfo) {
            Write-Host "  Found .NET 8.0 runtime:" -ForegroundColor Green
            Write-Host "    $dotnetInfo" -ForegroundColor Gray
            return $true
        }
        else {
            Write-Host "  .NET 8.0 runtime not found!" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "  Error checking .NET runtime: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to check Visual C++ Redistributable
function Test-VCRedist {
    Write-Host "Checking for Visual C++ Redistributable..." -ForegroundColor Yellow
    
    $vcRedistFound = $false
    $vcVersions = @()
    
    # Check registry for Visual C++ Redistributable
    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\15.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\15.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\16.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\16.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\17.0\VC\Runtimes\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\17.0\VC\Runtimes\*"
    )
    
    foreach ($path in $registryPaths) {
        try {
            $items = Get-Item $path -ErrorAction SilentlyContinue
            if ($items) {
                foreach ($item in $items) {
                    $version = $item.GetValue("Version")
                    if ($version) {
                        $vcVersions += $version
                        $vcRedistFound = $true
                    }
                }
            }
        }
        catch {
            # Ignore errors
        }
    }
    
    if ($vcRedistFound) {
        Write-Host "  Found Visual C++ Redistributable versions:" -ForegroundColor Green
        $vcVersions | Sort-Object -Unique | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        return $true
    }
    else {
        Write-Host "  Visual C++ Redistributable not found!" -ForegroundColor Red
        return $false
    }
}

# Function to check Windows version
function Test-WindowsVersion {
    Write-Host "Checking Windows version..." -ForegroundColor Yellow
    
    $osInfo = Get-CimInstance -ClassName Win32_OperatingSystem
    $buildNumber = [int]$osInfo.BuildNumber
    
    Write-Host "  OS: $($osInfo.Caption)" -ForegroundColor Gray
    Write-Host "  Build: $buildNumber" -ForegroundColor Gray
    
    if ($buildNumber -ge 17763) {
        Write-Host "  Windows version is compatible (requires 17763 or later)" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "  Windows version is NOT compatible (requires 17763 or later)" -ForegroundColor Red
        return $false
    }
}

# Function to clear MAUI cache
function Clear-MauICache {
    Write-Host ""
    Write-Host "Clearing MAUI cache..." -ForegroundColor Yellow
    
    $cachePaths = @(
        "$env:LOCALAPPDATA\Temp\Microsoft\VisualStudio",
        "$env:LOCALAPPDATA\Temp\Microsoft\MAUI",
        "$env:USERPROFILE\.nuget\packages\microsoft.maui"
    )
    
    foreach ($path in $cachePaths) {
        if (Test-Path $path) {
            Write-Host "  Clearing: $path" -ForegroundColor Gray
            try {
                Remove-Item -Path "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
                Write-Host "    Cleared successfully" -ForegroundColor Green
            }
            catch {
                Write-Host "    Warning: Could not clear: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    }
    
    Write-Host "MAUI cache cleared" -ForegroundColor Green
}

# Function to download and install Windows App SDK
function Install-WindowsAppSDK {
    Write-Host ""
    Write-Host "Windows App SDK Installation" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $downloadUrl = "https://aka.ms/windowsappsdk/1.4/1.4.231008000/windowsappruntimeinstall-x64.exe"
    $outputPath = "$env:TEMP\windowsappruntimeinstall-x64.exe"
    
    Write-Host "Download URL: $downloadUrl" -ForegroundColor Gray
    Write-Host "Output Path: $outputPath" -ForegroundColor Gray
    Write-Host ""
    
    $response = Read-Host "Do you want to download and install Windows App SDK 1.4.231008000? (Y/N)"
    if ($response -ne "Y" -and $response -ne "y") {
        Write-Host "Installation cancelled" -ForegroundColor Yellow
        return $false
    }
    
    Write-Host ""
    Write-Host "Downloading Windows App SDK runtime..." -ForegroundColor Yellow
    
    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $outputPath -UseBasicParsing
        Write-Host "Download completed: $outputPath" -ForegroundColor Green
    }
    catch {
        Write-Host "Error downloading Windows App SDK: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Please download manually from: $downloadUrl" -ForegroundColor Yellow
        return $false
    }
    
    Write-Host ""
    Write-Host "Installing Windows App SDK runtime..." -ForegroundColor Yellow
    Write-Host "A UAC prompt will appear. Please click 'Yes' to continue." -ForegroundColor Cyan
    
    try {
        Start-Process -FilePath $outputPath -Wait -Verb RunAs
        Write-Host "Installation completed" -ForegroundColor Green
        Write-Host ""
        Write-Host "IMPORTANT: Please restart your computer to complete the installation" -ForegroundColor Yellow
        return $true
    }
    catch {
        Write-Host "Error installing Windows App SDK: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    finally {
        # Clean up downloaded file
        if (Test-Path $outputPath) {
            Remove-Item -Path $outputPath -Force -ErrorAction SilentlyContinue
        }
    }
}

# Main diagnostic flow
Write-Host "Running diagnostics..." -ForegroundColor Yellow
Write-Host ""

# Check Windows version
$windowsCompatible = Test-WindowsVersion
Write-Host ""

# Check .NET runtime
$dotnetInstalled = Test-DotNetRuntime
Write-Host ""

# Check Visual C++ Redistributable
$vcRedistInstalled = Test-VCRedist
Write-Host ""

# Check Windows App SDK runtime
$windowsAppSDKInstalled = Test-WindowsAppSDK
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Diagnostic Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Windows Version: " -NoNewline
if ($windowsCompatible) {
    Write-Host "Compatible" -ForegroundColor Green
}
else {
    Write-Host "NOT Compatible" -ForegroundColor Red
}

Write-Host ".NET 8.0 Runtime: " -NoNewline
if ($dotnetInstalled) {
    Write-Host "Installed" -ForegroundColor Green
}
else {
    Write-Host "NOT Installed" -ForegroundColor Red
}

Write-Host "Visual C++ Redistributable: " -NoNewline
if ($vcRedistInstalled) {
    Write-Host "Installed" -ForegroundColor Green
}
else {
    Write-Host "NOT Installed" -ForegroundColor Red
}

Write-Host "Windows App SDK Runtime: " -NoNewline
if ($windowsAppSDKInstalled) {
    Write-Host "Installed" -ForegroundColor Green
}
else {
    Write-Host "NOT Installed" -ForegroundColor Red
}

Write-Host ""

# Provide recommendations
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Recommendations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $windowsCompatible) {
    Write-Host "❌ Your Windows version is not compatible with MAUI." -ForegroundColor Red
    Write-Host "   Please upgrade to Windows 10 version 1809 (Build 17763) or later." -ForegroundColor Yellow
    Write-Host ""
}

if (-not $dotnetInstalled) {
    Write-Host "❌ .NET 8.0 Runtime is not installed." -ForegroundColor Red
    Write-Host "   Please download and install from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    Write-Host ""
}

if (-not $vcRedistInstalled) {
    Write-Host "❌ Visual C++ Redistributable is not installed." -ForegroundColor Red
    Write-Host "   Please download and install from: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Yellow
    Write-Host ""
}

if (-not $windowsAppSDKInstalled) {
    Write-Host "❌ Windows App SDK Runtime is not installed." -ForegroundColor Red
    Write-Host "   This is the most likely cause of the COMException error." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To fix this issue, you have two options:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Option 1: Automatic Installation (Recommended)" -ForegroundColor Green
    Write-Host "  This script can download and install Windows App SDK 1.4.231008000 for you." -ForegroundColor Gray
    Write-Host ""
    Write-Host "Option 2: Manual Installation" -ForegroundColor Green
    Write-Host "  Download from: https://aka.ms/windowsappsdk/1.4/1.4.231008000/windowsappruntimeinstall-x64.exe" -ForegroundColor Gray
    Write-Host "  Or install via Visual Studio Installer:" -ForegroundColor Gray
    Write-Host "  1. Open Visual Studio Installer" -ForegroundColor Gray
    Write-Host "  2. Click 'Modify' on Visual Studio 2022" -ForegroundColor Gray
    Write-Host "  3. Navigate to 'Individual Components'" -ForegroundColor Gray
    Write-Host "  4. Search for 'Windows App SDK'" -ForegroundColor Gray
    Write-Host "  5. Install 'Windows App SDK C# Templates' and 'Windows App SDK Runtime'" -ForegroundColor Gray
    Write-Host ""
}

if ($windowsAppSDKInstalled) {
    Write-Host "✅ All required components are installed." -ForegroundColor Green
    Write-Host ""
    Write-Host "If you're still experiencing the COMException, try clearing the MAUI cache:" -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Do you want to clear the MAUI cache? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        Clear-MauICache
    }
    Write-Host ""
    Write-Host "After clearing the cache, rebuild your project:" -ForegroundColor Cyan
    Write-Host "  dotnet clean SyncUI/SyncUI.csproj" -ForegroundColor Gray
    Write-Host "  dotnet restore SyncUI/SyncUI.csproj" -ForegroundColor Gray
    Write-Host "  dotnet build SyncUI/SyncUI.csproj" -ForegroundColor Gray
    Write-Host ""
}
else {
    Write-Host ""
    $response = Read-Host "Do you want to install Windows App SDK Runtime now? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        $installSuccess = Install-WindowsAppSDK
        if ($installSuccess) {
            Write-Host ""
            Write-Host "Installation completed successfully!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Next steps:" -ForegroundColor Cyan
            Write-Host "1. Restart your computer" -ForegroundColor Gray
            Write-Host "2. Rebuild your project:" -ForegroundColor Gray
            Write-Host "   dotnet clean SyncUI/SyncUI.csproj" -ForegroundColor Gray
            Write-Host "   dotnet restore SyncUI/SyncUI.csproj" -ForegroundColor Gray
            Write-Host "   dotnet build SyncUI/SyncUI.csproj" -ForegroundColor Gray
            Write-Host "3. Run your application:" -ForegroundColor Gray
            Write-Host "   dotnet run --project SyncUI/SyncUI.csproj" -ForegroundColor Gray
        }
    }
    else {
        Write-Host ""
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To fix the COMException, you must install the Windows App SDK Runtime." -ForegroundColor Red
        Write-Host "Please download and install it from:" -ForegroundColor Yellow
        Write-Host "https://aka.ms/windowsappsdk/1.4/1.4.231008000/windowsappruntimeinstall-x64.exe" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Diagnostic Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to exit"

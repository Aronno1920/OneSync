using Microsoft.Extensions.Logging;
using SyncUI.Services;
using SyncUI.ViewModels;
using SyncUI.Views;
using System.Runtime.InteropServices;


namespace SyncUI;

public static class MauiProgram
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpLibFileName);

    private static void CheckWindowsAppSDKRuntime()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Checking for Windows App SDK runtime...");
            
            // Try to load the Windows App SDK runtime DLL
            var dllNames = new[]
            {
                "Microsoft.WindowsAppRuntime.1.4.dll",
                "Microsoft.WindowsAppRuntime.dll",
                "WindowsAppRuntime.dll"
            };
            
            bool runtimeFound = false;
            foreach (var dllName in dllNames)
            {
                var handle = LoadLibrary(dllName);
                if (handle != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"Found Windows App SDK runtime: {dllName}");
                    runtimeFound = true;
                    break;
                }
            }
            
            if (!runtimeFound)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: Windows App SDK runtime not found!");
                System.Diagnostics.Debug.WriteLine("Please install the Windows App SDK runtime from:");
                System.Diagnostics.Debug.WriteLine("https://aka.ms/windowsappsdk/1.4/1.4.231008000/windowsappruntimeinstall-x64.exe");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking Windows App SDK runtime: {ex.Message}");
        }
    }

    public static MauiApp CreateMauiApp()
    {
        try
        {
            // Check for Windows App SDK runtime availability
            CheckWindowsAppSDKRuntime();
            
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    // TODO: Add font files to Resources/Fonts/ directory
                    // fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    // fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Register Services
            builder.Services.AddSingleton<GrpcSyncClient>();
            builder.Services.AddSingleton<FileSystemWatcherService>();
            builder.Services.AddSingleton<NotificationService>();

            // Register ViewModels
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddTransient<SyncJobViewModel>();
            builder.Services.AddTransient<FileMonitorViewModel>();

            // Register Views
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<SyncJobPage>();
            builder.Services.AddTransient<FileListView>();

            var app = builder.Build();
            System.Diagnostics.Debug.WriteLine("MAUI App built successfully");
            return app;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== MAUI Build Error ===");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"HResult: 0x{ex.HResult:X8}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"\n=== Inner Exception ===");
                System.Diagnostics.Debug.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Message: {ex.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"HResult: 0x{ex.InnerException.HResult:X8}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace:\n{ex.InnerException.StackTrace}");
                
                if (ex.InnerException.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"\n=== Inner Inner Exception ===");
                    System.Diagnostics.Debug.WriteLine($"Type: {ex.InnerException.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"Message: {ex.InnerException.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"HResult: 0x{ex.InnerException.InnerException.HResult:X8}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"========================");
            
            // Also write to a file for persistent logging
            try
            {
                var logPath = Path.Combine(FileSystem.AppDataDirectory, "maui_error.log");
                var logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MAUI Build Error:\n" +
                               $"Exception Type: {ex.GetType().FullName}\n" +
                               $"Message: {ex.Message}\n" +
                               $"HResult: 0x{ex.HResult:X8}\n" +
                               $"Stack Trace:\n{ex.StackTrace}\n";
                if (ex.InnerException != null)
                {
                    logContent += $"\nInner Exception:\n" +
                                $"Type: {ex.InnerException.GetType().FullName}\n" +
                                $"Message: {ex.InnerException.Message}\n" +
                                $"HResult: 0x{ex.InnerException.HResult:X8}\n";
                }
                
                // Ensure directory exists before writing
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                File.WriteAllText(logPath, logContent);
                System.Diagnostics.Debug.WriteLine($"Error log written to: {logPath}");
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write error log: {logEx.Message}");
            }
            
            throw;
        }
    }
}

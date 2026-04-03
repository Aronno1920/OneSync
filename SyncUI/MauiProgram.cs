using Microsoft.Extensions.Logging;
using SyncUI.Services;
using SyncUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace SyncUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
                    try
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Register services
            builder.Services.AddSingleton<GrpcSyncClient>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<FileSystemWatcherService>();

            // Register view models
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<SyncJobViewModel>();
            builder.Services.AddTransient<FileMonitorViewModel>();

            // Register pages
            builder.Services.AddTransient<Views.MainPage>();
            builder.Services.AddTransient<Views.SyncJobPage>();
            builder.Services.AddTransient<Views.FileListView>();

System.Diagnostics.Debug.WriteLine("MAUI App built successfully");

            return builder.Build();
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

                //Selim
                string errorDesc = logContent;

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
}

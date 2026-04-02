using Microsoft.Extensions.Logging;
using SyncUI.Services;
using SyncUI.ViewModels;
using SyncUI.Views;

namespace SyncUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
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

        return builder.Build();
    }
}

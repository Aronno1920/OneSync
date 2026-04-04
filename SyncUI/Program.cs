using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SyncUI.Forms;
using SyncUI.Services;
using SyncUI.ViewModels;

namespace SyncUI
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Set up dependency injection
            var serviceProvider = ConfigureServices();

            // Run the main form
            var mainForm = serviceProvider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register services
            services.AddSingleton<GrpcSyncClient>();
            services.AddSingleton<NotificationService>();

            // Register view models
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SyncJobViewModel>();

            // Register forms
            services.AddSingleton<MainForm>();
            services.AddTransient<SyncJobForm>();
            services.AddTransient<FileListForm>();

            return services.BuildServiceProvider();
        }
    }
}
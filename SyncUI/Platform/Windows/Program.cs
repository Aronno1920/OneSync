using Microsoft.UI.Xaml;

namespace SyncUI.WinUI;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var context = new MauiWinUIApplicationContext();
            context.LaunchActivated += (s, e) =>
            {
                var app = new App();
                app.Run(e);
            };
            return context;
        });
    }
}

internal class MauiWinUIApplicationContext : MauiWinUIApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

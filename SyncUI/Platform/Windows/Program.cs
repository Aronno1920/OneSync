#if WINDOWS
using Microsoft.UI.Xaml;

namespace SyncUI.WinUI;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new MauiWinUIApplicationContext();
        });
    }
}

internal class MauiWinUIApplicationContext : MauiWinUIApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
#endif

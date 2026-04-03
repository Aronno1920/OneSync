using Microsoft.Maui.Controls;
using SyncUI.Converters;

namespace SyncUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Register converters in resources
        RegisterConverters();

        MainPage = new AppShell();
    }

    private void RegisterConverters()
    {
        Resources["BoolToTitleConverter"] = new BoolToTitleConverter();
        Resources["InverseBoolConverter"] = new InverseBoolConverter();
        Resources["BoolToValidationColorConverter"] = new BoolToValidationColorConverter();
        Resources["BoolToValidationTextColorConverter"] = new BoolToValidationTextColorConverter();
        Resources["TimeSpanToMinutesConverter"] = new TimeSpanToMinutesConverter();
        Resources["TimeSpanToTextConverter"] = new TimeSpanToTextConverter();
        Resources["PercentToDoubleConverter"] = new PercentToDoubleConverter();
        Resources["StatusToColorConverter"] = new StatusToColorConverter();
        Resources["FileStatusToColorConverter"] = new FileStatusToColorConverter();
    }
}

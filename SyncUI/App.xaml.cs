using Microsoft.Maui.Controls;
using SyncUI.Converters;

namespace SyncUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Load resources programmatically to avoid COMException
        LoadResources();

        MainPage = new AppShell();
    }

    private void LoadResources()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Loading resources programmatically...");

            // Load merged resource dictionaries
            var colors = new ResourceDictionary { Source = new Uri("Resources/Styles/Colors.xaml", UriKind.Relative) };
            var styles = new ResourceDictionary { Source = new Uri("Resources/Styles/Styles.xaml", UriKind.Relative) };

            Resources.MergedDictionaries.Add(colors);
            Resources.MergedDictionaries.Add(styles);

            // Add converters
            Resources["BoolToTitleConverter"] = new BoolToTitleConverter();
            Resources["InverseBoolConverter"] = new InverseBoolConverter();
            Resources["BoolToValidationColorConverter"] = new BoolToValidationColorConverter();
            Resources["BoolToValidationTextColorConverter"] = new BoolToValidationTextColorConverter();
            Resources["TimeSpanToMinutesConverter"] = new TimeSpanToMinutesConverter();
            Resources["TimeSpanToTextConverter"] = new TimeSpanToTextConverter();
            Resources["PercentToDoubleConverter"] = new PercentToDoubleConverter();
            Resources["StatusToColorConverter"] = new StatusToColorConverter();
            Resources["FileStatusToColorConverter"] = new FileStatusToColorConverter();

            System.Diagnostics.Debug.WriteLine("Resources loaded successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading resources: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            // Don't throw - allow app to continue even if resources fail to load
        }
    }
}

using Microsoft.Maui.Controls;

namespace SyncUI.Converters;

public class BoolToTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isNew)
        {
            return isNew ? "Create New Sync Job" : "Edit Sync Job";
        }
        return "Sync Job";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using Microsoft.Maui.Controls;
using SyncUI.Models;

namespace SyncUI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is JobStatus status)
        {
            return status switch
            {
                JobStatus.Idle => Colors.Gray,
                JobStatus.Scanning => Colors.Blue,
                JobStatus.Syncing => Colors.Green,
                JobStatus.Paused => Colors.Yellow,
                JobStatus.Completed => Colors.Green,
                JobStatus.Failed => Colors.Red,
                JobStatus.Conflict => Colors.Orange,
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FileSyncStatus status)
        {
            return status switch
            {
                FileSyncStatus.Unknown => Colors.Gray,
                FileSyncStatus.InSync => Colors.Green,
                FileSyncStatus.New => Colors.Blue,
                FileSyncStatus.Modified => Colors.Orange,
                FileSyncStatus.Deleted => Colors.Red,
                FileSyncStatus.Syncing => Colors.Blue,
                FileSyncStatus.Synced => Colors.Green,
                FileSyncStatus.Failed => Colors.Red,
                FileSyncStatus.Skipped => Colors.Gray,
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

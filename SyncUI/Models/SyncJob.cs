using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncUI.Models;

/// <summary>
/// Represents a synchronization job configuration
/// </summary>
public class SyncJob : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private SyncDirection _direction = SyncDirection.Bidirectional;
    private SyncMode _mode = SyncMode.Automatic;
    private JobStatus _status = JobStatus.Idle;
    private bool _isEnabled = true;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    private int _totalFiles = 0;
    private int _syncedFiles = 0;
    private int _failedFiles = 0;
    private long _totalBytes = 0;
    private long _transferredBytes = 0;
    private double _progress = 0.0;
    private string? _errorMessage;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    public SyncDirection Direction
    {
        get => _direction;
        set => SetProperty(ref _direction, value);
    }

    public SyncMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public JobStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public DateTime LastSyncTime
    {
        get => _lastSyncTime;
        set => SetProperty(ref _lastSyncTime, value);
    }

    public TimeSpan SyncInterval
    {
        get => _syncInterval;
        set => SetProperty(ref _syncInterval, value);
    }

    public int TotalFiles
    {
        get => _totalFiles;
        set => SetProperty(ref _totalFiles, value);
    }

    public int SyncedFiles
    {
        get => _syncedFiles;
        set => SetProperty(ref _syncedFiles, value);
    }

    public int FailedFiles
    {
        get => _failedFiles;
        set => SetProperty(ref _failedFiles, value);
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set => SetProperty(ref _totalBytes, value);
    }

    public long TransferredBytes
    {
        get => _transferredBytes;
        set => SetProperty(ref _transferredBytes, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string StatusText => Status switch
    {
        JobStatus.Idle => "Idle",
        JobStatus.Scanning => "Scanning...",
        JobStatus.Syncing => "Syncing...",
        JobStatus.Paused => "Paused",
        JobStatus.Completed => "Completed",
        JobStatus.Failed => "Failed",
        JobStatus.Conflict => "Conflicts Detected",
        _ => "Unknown"
    };

    public string DirectionText => Direction switch
    {
        SyncDirection.SourceToDestination => "Source → Destination",
        SyncDirection.DestinationToSource => "Destination → Source",
        SyncDirection.Bidirectional => "Bidirectional",
        _ => "Unknown"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ResetProgress()
    {
        TotalFiles = 0;
        SyncedFiles = 0;
        FailedFiles = 0;
        TotalBytes = 0;
        TransferredBytes = 0;
        Progress = 0.0;
        ErrorMessage = null;
    }

    public void UpdateProgress(int synced, int failed, long transferred)
    {
        SyncedFiles = synced;
        FailedFiles = failed;
        TransferredBytes = transferred;
        
        if (TotalBytes > 0)
        {
            Progress = (double)TransferredBytes / TotalBytes * 100.0;
        }
    }
}

/// <summary>
/// Defines the direction of synchronization
/// </summary>
public enum SyncDirection
{
    SourceToDestination,
    DestinationToSource,
    Bidirectional
}

/// <summary>
/// Defines the synchronization mode
/// </summary>
public enum SyncMode
{
    Manual,
    Automatic,
    Scheduled
}

/// <summary>
/// Defines the status of a sync job
/// </summary>
public enum JobStatus
{
    Idle,
    Scanning,
    Syncing,
    Paused,
    Completed,
    Failed,
    Conflict
}

using SyncUI.Models;
using SyncUI.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncUI.ViewModels;

/// <summary>
/// View model for sync job configuration and management
/// </summary>
public class SyncJobViewModel : INotifyPropertyChanged
{
    private readonly GrpcSyncClient _syncClient;
    private readonly NotificationService _notificationService;

    private SyncJob _job = new();
    private bool _isNew = true;
    private bool _isLoading;
    private bool _isValid;
    private string _validationMessage = string.Empty;
    private bool _isTestingConnection;

    public SyncJob Job
    {
        get => _job;
        set
        {
            if (SetProperty(ref _job, value))
            {
                Validate();
            }
        }
    }

    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool IsTestingConnection
    {
        get => _isTestingConnection;
        set => SetProperty(ref _isTestingConnection, value);
    }

    public List<string> AvailableDirections { get; } = Enum.GetNames(typeof(SyncDirection)).ToList();
    public List<string> AvailableModes { get; } = Enum.GetNames(typeof(SyncMode)).ToList();

    public SyncJobViewModel(
        GrpcSyncClient syncClient,
        NotificationService notificationService)
    {
        _syncClient = syncClient;
        _notificationService = notificationService;
    }

    public void Initialize(SyncJob? job, bool isNew)
    {
        if (job != null)
        {
            Job = job;
        }
        IsNew = isNew;
        Validate();
    }

    public void SetSourcePath(string path)
    {
        Job.SourcePath = path;
        Validate();
    }

    public void SetDestinationPath(string path)
    {
        Job.DestinationPath = path;
        Validate();
    }

    public async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(Job.SourcePath) || string.IsNullOrWhiteSpace(Job.DestinationPath))
        {
            ValidationMessage = "Please specify both source and destination paths";
            return;
        }

        IsTestingConnection = true;
        try
        {
            var sourceExists = Directory.Exists(Job.SourcePath);
            var destExists = Directory.Exists(Job.DestinationPath);

            if (sourceExists && destExists)
            {
                ValidationMessage = "✓ Both paths are accessible";
            }
            else if (!sourceExists)
            {
                ValidationMessage = "✗ Source path does not exist";
            }
            else
            {
                ValidationMessage = "✗ Destination path does not exist";
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = $"✗ Error: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    public async Task<SyncJob?> SaveAsync()
    {
        if (!IsValid) return null;

        IsLoading = true;
        try
        {
            if (IsNew)
            {
                Job = await _syncClient.CreateJobAsync(Job);
                await _notificationService.ShowNotificationAsync("Job Created", $"Job '{Job.Name}' has been created");
            }
            else
            {
                // TODO: Implement update job functionality when backend supports it
                await _notificationService.ShowNotificationAsync("Job Updated", $"Job '{Job.Name}' has been updated (local only)");
            }

            return Job;
        }
        catch (Exception ex)
        {
            await _notificationService.ShowErrorNotificationAsync("Save", ex.Message);
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Job.Name))
        {
            errors.Add("Job name is required");
        }

        if (string.IsNullOrWhiteSpace(Job.SourcePath))
        {
            errors.Add("Source path is required");
        }
        else if (!Directory.Exists(Job.SourcePath))
        {
            errors.Add("Source path does not exist");
        }

        if (string.IsNullOrWhiteSpace(Job.DestinationPath))
        {
            errors.Add("Destination path is required");
        }
        else if (!Directory.Exists(Job.DestinationPath))
        {
            errors.Add("Destination path does not exist");
        }

        if (Job.SourcePath.Equals(Job.DestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Source and destination paths cannot be the same");
        }

        if (Job.SyncInterval.TotalSeconds < 60)
        {
            errors.Add("Sync interval must be at least 1 minute");
        }

        IsValid = errors.Count == 0;
        ValidationMessage = IsValid ? "All settings are valid" : string.Join("\n", errors);
    }

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
}

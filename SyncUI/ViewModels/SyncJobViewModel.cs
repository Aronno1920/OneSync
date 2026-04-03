using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SyncUI.Models;
using SyncUI.Services;

namespace SyncUI.ViewModels;

/// <summary>
/// View model for sync job configuration and management
/// </summary>
public partial class SyncJobViewModel : ObservableObject
{
    private readonly GrpcSyncClient _syncClient;
    private readonly NotificationService _notificationService;
    private readonly ILogger<SyncJobViewModel> _logger;

    [ObservableProperty]
    private SyncJob _job = new();

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isTestingConnection;

    public List<string> AvailableDirections { get; } = Enum.GetNames(typeof(SyncDirection)).ToList();
    public List<string> AvailableModes { get; } = Enum.GetNames(typeof(SyncMode)).ToList();

    public SyncJobViewModel(
        GrpcSyncClient syncClient,
        NotificationService notificationService,
        ILogger<SyncJobViewModel> logger)
    {
        _syncClient = syncClient;
        _notificationService = notificationService;
        _logger = logger;
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

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Source Folder",
                FileTypes = null
            });

            if (result != null)
            {
                var directory = Path.GetDirectoryName(result.FullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Job.SourcePath = directory;
                    Validate();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse source folder");
            await _notificationService.ShowErrorNotificationAsync("Browse", ex.Message);
        }
    }

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Destination Folder",
                FileTypes = null
            });

            if (result != null)
            {
                var directory = Path.GetDirectoryName(result.FullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Job.DestinationPath = directory;
                    Validate();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse destination folder");
            await _notificationService.ShowErrorNotificationAsync("Browse", ex.Message);
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
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
            _logger.LogError(ex, "Failed to test connection");
            ValidationMessage = $"✗ Error: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsValid) return;

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
                // Job = await _syncClient.UpdateJobAsync(Job);
                await _notificationService.ShowNotificationAsync("Job Updated", $"Job '{Job.Name}' has been updated (local only)");
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save job");
            await _notificationService.ShowErrorNotificationAsync("Save", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void Validate()
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

    partial void OnJobChanged(SyncJob value)
    {
        Validate();
    }
}

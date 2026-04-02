using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncUI.Models;
using SyncUI.Services;
using System.Collections.ObjectModel;

namespace SyncUI.ViewModels;

/// <summary>
/// Main view model for the application
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly GrpcSyncClient _syncClient;
    private readonly NotificationService _notificationService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Connecting to sync engine...";

    [ObservableProperty]
    private int _activeJobsCount;

    [ObservableProperty]
    private int _totalJobsCount;

    [ObservableProperty]
    private string _totalTransferredSize = "0 B";

    public ObservableCollection<SyncJob> Jobs { get; } = new();

    public MainViewModel(
        GrpcSyncClient syncClient,
        NotificationService notificationService,
        ILogger<MainViewModel> logger)
    {
        _syncClient = syncClient;
        _notificationService = notificationService;
        _logger = logger;

        // Subscribe to sync client events
        _syncClient.SyncProgress += OnSyncProgress;
        _syncClient.SyncComplete += OnSyncComplete;
        _syncClient.SyncError += OnSyncError;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Connecting to sync engine...";

        try
        {
            var connected = await _syncClient.ConnectAsync();
            IsConnected = connected;

            if (connected)
            {
                StatusMessage = "Connected. Loading jobs...";
                await LoadJobsAsync();
                StatusMessage = "Ready";
            }
            else
            {
                StatusMessage = "Failed to connect to sync engine";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main view model");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshJobsAsync()
    {
        if (!IsConnected) return;

        IsLoading = true;
        try
        {
            await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh jobs");
            await _notificationService.ShowErrorNotificationAsync("Refresh", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateNewJobAsync()
    {
        if (!IsConnected) return;

        // Navigate to job creation page
        // This would typically use Shell navigation
        await Shell.Current.GoToAsync("//SyncJobPage", new Dictionary<string, object>
        {
            { "Job", null },
            { "IsNew", true }
        });
    }

    [RelayCommand]
    private async Task EditJobAsync(SyncJob? job)
    {
        if (job == null || !IsConnected) return;

        await Shell.Current.GoToAsync("//SyncJobPage", new Dictionary<string, object>
        {
            { "Job", job },
            { "IsNew", false }
        });
    }

    [RelayCommand]
    private async Task DeleteJobAsync(SyncJob? job)
    {
        if (job == null || !IsConnected) return;

        var confirmed = await Application.Current?.MainPage?.DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete job '{job.Name}'?",
            "Delete",
            "Cancel") ?? false;

        if (!confirmed) return;

        IsLoading = true;
        try
        {
            var success = await _syncClient.DeleteJobAsync(job.Id);
            if (success)
            {
                Jobs.Remove(job);
                UpdateStatistics();
                await _notificationService.ShowNotificationAsync("Job Deleted", $"Job '{job.Name}' has been deleted");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job {JobId}", job.Id);
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StartJobAsync(SyncJob? job)
    {
        if (job == null || !IsConnected) return;

        IsLoading = true;
        try
        {
            var success = await _syncClient.StartJobAsync(job.Id);
            if (success)
            {
                job.Status = JobStatus.Syncing;
                await _notificationService.ShowNotificationAsync("Sync Started", $"Job '{job.Name}' has started");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start job {JobId}", job.Id);
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StopJobAsync(SyncJob? job)
    {
        if (job == null || !IsConnected) return;

        IsLoading = true;
        try
        {
            var success = await _syncClient.StopJobAsync(job.Id);
            if (success)
            {
                job.Status = JobStatus.Idle;
                await _notificationService.ShowNotificationAsync("Sync Stopped", $"Job '{job.Name}' has been stopped");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop job {JobId}", job.Id);
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PauseJobAsync(SyncJob? job)
    {
        if (job == null || !IsConnected) return;

        IsLoading = true;
        try
        {
            var success = await _syncClient.PauseJobAsync(job.Id);
            if (success)
            {
                job.Status = JobStatus.Paused;
                await _notificationService.ShowNotificationAsync("Sync Paused", $"Job '{job.Name}' has been paused");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause job {JobId}", job.Id);
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ViewJobFilesAsync(SyncJob? job)
    {
        if (job == null) return;

        await Shell.Current.GoToAsync("//FileListView", new Dictionary<string, object>
        {
            { "Job", job }
        });
    }

    private async Task LoadJobsAsync()
    {
        var jobs = await _syncClient.GetJobsAsync();
        Jobs.Clear();
        foreach (var job in jobs)
        {
            Jobs.Add(job);
        }
        UpdateStatistics();
    }

    private void UpdateStatistics()
    {
        TotalJobsCount = Jobs.Count;
        ActiveJobsCount = Jobs.Count(j => j.Status == JobStatus.Syncing || j.Status == JobStatus.Scanning);
        
        var totalBytes = Jobs.Sum(j => j.TransferredBytes);
        TotalTransferredSize = FormatBytes(totalBytes);
    }

    private void OnSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        var job = Jobs.FirstOrDefault(j => j.Id == e.JobId);
        if (job != null)
        {
            job.UpdateProgress(e.SyncedFiles, e.FailedFiles, e.TransferredBytes);
            job.Progress = e.Progress;
            job.TotalFiles = e.TotalFiles;
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateStatistics();
            });
        }
    }

    private async void OnSyncComplete(object? sender, SyncCompleteEventArgs e)
    {
        var job = Jobs.FirstOrDefault(j => j.Id == e.JobId);
        if (job != null)
        {
            job.Status = e.Success ? JobStatus.Completed : JobStatus.Failed;
            job.ErrorMessage = e.ErrorMessage;
            
            await _notificationService.ShowSyncCompleteNotificationAsync(
                job.Name,
                e.Success,
                e.ErrorMessage);
        }

        await RefreshJobsAsync();
    }

    private async void OnSyncError(object? sender, SyncErrorEventArgs e)
    {
        var job = Jobs.FirstOrDefault(j => j.Id == e.JobId);
        if (job != null)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = e.ErrorMessage;
        }

        await _notificationService.ShowErrorNotificationAsync(
            job?.Name ?? "Unknown Job",
            e.ErrorMessage);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

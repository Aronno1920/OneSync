using SyncUI.Models;
using SyncUI.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncUI.ViewModels;

/// <summary>
/// Main view model for the application
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly GrpcSyncClient _syncClient;
    private readonly NotificationService _notificationService;

    private bool _isConnected;
    private bool _isLoading;
    private string _statusMessage = "Connecting to sync engine...";
    private int _activeJobsCount;
    private int _totalJobsCount;
    private string _totalTransferredSize = "0 B";

    public BindingList<SyncJob> Jobs { get; } = new();

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int ActiveJobsCount
    {
        get => _activeJobsCount;
        set => SetProperty(ref _activeJobsCount, value);
    }

    public int TotalJobsCount
    {
        get => _totalJobsCount;
        set => SetProperty(ref _totalJobsCount, value);
    }

    public string TotalTransferredSize
    {
        get => _totalTransferredSize;
        set => SetProperty(ref _totalTransferredSize, value);
    }

    public MainViewModel(GrpcSyncClient syncClient, NotificationService notificationService)
    {
        _syncClient = syncClient;
        _notificationService = notificationService;

        // Subscribe to sync client events
        _syncClient.SyncProgress += OnSyncProgress;
        _syncClient.SyncComplete += OnSyncComplete;
        _syncClient.SyncError += OnSyncError;
    }

    public async Task InitializeAsync()
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
                StatusMessage = "Failed to connect to sync engine. Please ensure the sync-engine server is running.";
                StatusMessage += " Run 'start-app.bat' to start both the server and UI.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RefreshJobsAsync()
    {
        if (!IsConnected) return;

        IsLoading = true;
        try
        {
            await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            await _notificationService.ShowErrorNotificationAsync("Refresh", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CreateNewJobAsync(SyncJob job)
    {
        if (!IsConnected) return;

        IsLoading = true;
        try
        {
            var createdJob = await _syncClient.CreateJobAsync(job);
            Jobs.Add(createdJob);
            UpdateStatistics();
            await _notificationService.ShowNotificationAsync("Job Created", $"Job '{job.Name}' has been created");
        }
        catch (Exception ex)
        {
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteJobAsync(SyncJob job)
    {
        if (job == null || !IsConnected) return;

        IsLoading = true;
        try
        {
            // TODO: Implement delete job functionality when backend supports it
            // For now, just remove from local list
            Jobs.Remove(job);
            UpdateStatistics();
            await _notificationService.ShowNotificationAsync("Job Deleted", $"Job '{job.Name}' has been deleted (local only)");
        }
        catch (Exception ex)
        {
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task StartJobAsync(SyncJob job)
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
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task StopJobAsync(SyncJob job)
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
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task PauseJobAsync(SyncJob job)
    {
        if (job == null || !IsConnected) return;

        IsLoading = true;
        try
        {
            // TODO: Implement pause job functionality when backend supports it
            // For now, use stop as a workaround
            var success = await _syncClient.StopJobAsync(job.Id);
            if (success)
            {
                job.Status = JobStatus.Paused;
                await _notificationService.ShowNotificationAsync("Sync Paused", $"Job '{job.Name}' has been paused");
            }
        }
        catch (Exception ex)
        {
            await _notificationService.ShowErrorNotificationAsync(job.Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
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
            
            // Update statistics on UI thread
            UpdateStatistics();
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

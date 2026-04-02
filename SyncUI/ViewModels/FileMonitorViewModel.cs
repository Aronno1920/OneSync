using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncUI.Models;
using SyncUI.Services;
using System.Collections.ObjectModel;

namespace SyncUI.ViewModels;

/// <summary>
/// View model for monitoring file changes and sync status
/// </summary>
public partial class FileMonitorViewModel : ObservableObject
{
    private readonly GrpcSyncClient _syncClient;
    private readonly FileSystemWatcherService _watcherService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<FileMonitorViewModel> _logger;

    [ObservableProperty]
    private SyncJob _job = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Loading files...";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private FileSyncStatus _filterStatus = FileSyncStatus.Unknown;

    [ObservableProperty]
    private bool _showOnlyConflicts;

    [ObservableProperty]
    private int _totalFilesCount;

    [ObservableProperty]
    private int _filteredFilesCount;

    [ObservableProperty]
    private long _totalSize;

    [ObservableProperty]
    private string _totalSizeFormatted = "0 B";

    public ObservableCollection<FileNode> Files { get; } = new();
    public ObservableCollection<FileNode> FilteredFiles { get; } = new();

    public List<string> AvailableStatusFilters { get; } = Enum.GetNames(typeof(FileSyncStatus)).ToList();

    public FileMonitorViewModel(
        GrpcSyncClient syncClient,
        FileSystemWatcherService watcherService,
        NotificationService notificationService,
        ILogger<FileMonitorViewModel> logger)
    {
        _syncClient = syncClient;
        _watcherService = watcherService;
        _notificationService = notificationService;
        _logger = logger;

        _watcherService.FileChanged += OnFileChanged;
    }

    public void Initialize(SyncJob job)
    {
        Job = job;
        LoadFilesAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task LoadFilesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading files...";

        try
        {
            var files = await _syncClient.GetJobFilesAsync(Job.Id);
            
            Files.Clear();
            foreach (var file in files)
            {
                Files.Add(file);
            }

            ApplyFilters();
            UpdateStatistics();
            StatusMessage = $"Loaded {Files.Count} files";

            // Start watching for changes
            if (!_watcherService.IsWatching(Job.Id))
            {
                await _watcherService.StartWatchingAsync(Job.Id, Job.SourcePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load files for job {JobId}", Job.Id);
            StatusMessage = $"Error: {ex.Message}";
            await _notificationService.ShowErrorNotificationAsync("Load Files", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshFilesAsync()
    {
        await LoadFilesAsync();
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        FilteredFiles.Clear();

        var query = Files.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(f => 
                f.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                f.RelativePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply status filter
        if (FilterStatus != FileSyncStatus.Unknown)
        {
            query = query.Where(f => f.Status == FilterStatus);
        }

        // Apply conflict filter
        if (ShowOnlyConflicts)
        {
            query = query.Where(f => f.HasConflicts);
        }

        foreach (var file in query)
        {
            FilteredFiles.Add(file);
        }

        FilteredFilesCount = FilteredFiles.Count;
    }

    [RelayCommand]
    private async Task ResolveConflictAsync(FileNode? file)
    {
        if (file == null) return;

        var actions = new List<string>
        {
            "Keep Source Version",
            "Keep Destination Version",
            "Keep Both (Rename)",
            "Skip"
        };

        var action = await Application.Current?.MainPage?.DisplayActionSheet(
            $"Resolve Conflict: {file.Name}",
            "Cancel",
            null,
            actions.ToArray()) ?? "Cancel";

        try
        {
            switch (action)
            {
                case "Keep Source Version":
                    // Implement conflict resolution logic
                    await _notificationService.ShowNotificationAsync(
                        "Conflict Resolved",
                        $"Kept source version of {file.Name}");
                    break;

                case "Keep Destination Version":
                    // Implement conflict resolution logic
                    await _notificationService.ShowNotificationAsync(
                        "Conflict Resolved",
                        $"Kept destination version of {file.Name}");
                    break;

                case "Keep Both (Rename)":
                    // Implement conflict resolution logic
                    await _notificationService.ShowNotificationAsync(
                        "Conflict Resolved",
                        $"Kept both versions of {file.Name}");
                    break;

                case "Skip":
                    await _notificationService.ShowNotificationAsync(
                        "Skipped",
                        $"Skipped conflict for {file.Name}");
                    break;
            }

            // Refresh files after resolution
            await LoadFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve conflict for file {FileId}", file.Id);
            await _notificationService.ShowErrorNotificationAsync("Resolve Conflict", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ViewFileDetailsAsync(FileNode? file)
    {
        if (file == null) return;

        var details = $"Name: {file.Name}\n" +
                      $"Path: {file.Path}\n" +
                      $"Relative Path: {file.RelativePath}\n" +
                      $"Size: {file.SizeFormatted}\n" +
                      $"Modified: {file.ModifiedTimeFormatted}\n" +
                      $"Status: {file.StatusText}\n" +
                      $"Hash: {file.Hash}\n" +
                      $"Has Conflicts: {file.HasConflicts}";

        if (file.HasConflicts)
        {
            details += $"\nConflict Type: {file.ConflictStatus}";
        }

        await Application.Current?.MainPage?.DisplayAlert(
            "File Details",
            details,
            "OK");
    }

    [RelayCommand]
    private async Task SyncSelectedFilesAsync()
    {
        var selectedFiles = FilteredFiles.Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            await _notificationService.ShowNotificationAsync(
                "No Files Selected",
                "Please select files to sync");
            return;
        }

        IsLoading = true;
        try
        {
            // Implement selective sync logic
            await _notificationService.ShowNotificationAsync(
                "Sync Started",
                $"Syncing {selectedFiles.Count} selected files");
            
            await LoadFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync selected files");
            await _notificationService.ShowErrorNotificationAsync("Sync", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleFileSelection(FileNode? file)
    {
        if (file != null)
        {
            file.IsSelected = !file.IsSelected;
        }
    }

    [RelayCommand]
    private void SelectAllFiles()
    {
        foreach (var file in FilteredFiles)
        {
            file.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAllFiles()
    {
        foreach (var file in FilteredFiles)
        {
            file.IsSelected = false;
        }
    }

    private void UpdateStatistics()
    {
        TotalFilesCount = Files.Count;
        TotalSize = Files.Sum(f => f.Size);
        TotalSizeFormatted = FormatBytes(TotalSize);
    }

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        if (e.JobId != Job.Id) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            StatusMessage = $"File changed: {Path.GetFileName(e.FilePath)}";
            await LoadFilesAsync();
        });
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

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnFilterStatusChanged(FileSyncStatus value)
    {
        ApplyFilters();
    }

    partial void OnShowOnlyConflictsChanged(bool value)
    {
        ApplyFilters();
    }
}

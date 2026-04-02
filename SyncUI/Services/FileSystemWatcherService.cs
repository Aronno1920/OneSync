using SyncUI.Models;

namespace SyncUI.Services;

/// <summary>
/// Service for monitoring file system changes
/// </summary>
public class FileSystemWatcherService : IDisposable
{
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public event EventHandler<FileChangedEventArgs>? FileChanged;
    public event EventHandler<WatcherErrorEventArgs>? WatcherError;

    /// <summary>
    /// Starts watching a directory for changes
    /// </summary>
    public async Task<bool> StartWatchingAsync(string jobId, string path, bool includeSubdirectories = true)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_watchers.ContainsKey(jobId))
            {
                await StopWatchingAsync(jobId);
            }

            if (!Directory.Exists(path))
            {
                Debug.WriteLine($"Directory does not exist: {path}");
                return false;
            }

            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.Attributes |
                               NotifyFilters.CreationTime |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.FileName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size,
                IncludeSubdirectories = includeSubdirectories,
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreated;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnWatcherError;

            _watchers[jobId] = watcher;
            Debug.WriteLine($"Started watching: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start watching {path}: {ex.Message}");
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Stops watching a directory
    /// </summary>
    public async Task<bool> StopWatchingAsync(string jobId)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_watchers.TryGetValue(jobId, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFileCreated;
                watcher.Changed -= OnFileChanged;
                watcher.Deleted -= OnFileDeleted;
                watcher.Renamed -= OnFileRenamed;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
                _watchers.Remove(jobId);
                Debug.WriteLine($"Stopped watching for job: {jobId}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop watching for job {jobId}: {ex.Message}");
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Stops all active watchers
    /// </summary>
    public async Task StopAllWatchersAsync()
    {
        var jobIds = _watchers.Keys.ToList();
        foreach (var jobId in jobIds)
        {
            await StopWatchingAsync(jobId);
        }
    }

    /// <summary>
    /// Checks if a directory is being watched
    /// </summary>
    public bool IsWatching(string jobId)
    {
        return _watchers.ContainsKey(jobId);
    }

    /// <summary>
    /// Gets all currently watched job IDs
    /// </summary>
    public IEnumerable<string> GetWatchedJobIds()
    {
        return _watchers.Keys;
    }

    /// <summary>
    /// Gets the path being watched for a specific job
    /// </summary>
    public string? GetWatchedPath(string jobId)
    {
        return _watchers.TryGetValue(jobId, out var watcher) ? watcher.Path : null;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var jobId = _watchers.FirstOrDefault(kvp => kvp.Value == sender).Key;
        if (!string.IsNullOrEmpty(jobId))
        {
            OnFileChangedEvent(jobId, e.FullPath, FileChangeType.Created);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var jobId = _watchers.FirstOrDefault(kvp => kvp.Value == sender).Key;
        if (!string.IsNullOrEmpty(jobId))
        {
            // Debounce rapid changes
            Task.Run(async () =>
            {
                await Task.Delay(500); // Wait 500ms to debounce
                OnFileChangedEvent(jobId, e.FullPath, FileChangeType.Modified);
            });
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var jobId = _watchers.FirstOrDefault(kvp => kvp.Value == sender).Key;
        if (!string.IsNullOrEmpty(jobId))
        {
            OnFileChangedEvent(jobId, e.FullPath, FileChangeType.Deleted);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var jobId = _watchers.FirstOrDefault(kvp => kvp.Value == sender).Key;
        if (!string.IsNullOrEmpty(jobId))
        {
            OnFileChangedEvent(jobId, e.FullPath, FileChangeType.Renamed);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var jobId = _watchers.FirstOrDefault(kvp => kvp.Value == sender).Key;
        if (!string.IsNullOrEmpty(jobId))
        {
            WatcherError?.Invoke(this, new WatcherErrorEventArgs
            {
                JobId = jobId,
                Error = e.GetException()
            });
        }
    }

    private void OnFileChangedEvent(string jobId, string filePath, FileChangeType changeType)
    {
        FileChanged?.Invoke(this, new FileChangedEventArgs
        {
            JobId = jobId,
            FilePath = filePath,
            ChangeType = changeType
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopAllWatchersAsync().GetAwaiter().GetResult();
            _semaphore.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for watcher errors
/// </summary>
public class WatcherErrorEventArgs : EventArgs
{
    public string JobId { get; set; } = string.Empty;
    public Exception? Error { get; set; }
}

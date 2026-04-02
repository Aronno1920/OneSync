using Grpc.Net.Client;
using SyncUI.Models;
using System.Diagnostics;

namespace SyncUI.Services;

/// <summary>
/// gRPC client for communicating with the Rust sync engine
/// </summary>
public class GrpcSyncClient : IDisposable
{
    private GrpcChannel? _channel;
    private SyncEngine.SyncEngineClient? _client;
    private readonly string _serverAddress;
    private bool _disposed;

    public bool IsConnected => _channel?.State == Grpc.Core.ConnectivityState.Ready;

    public event EventHandler<SyncProgressEventArgs>? SyncProgress;
    public event EventHandler<SyncCompleteEventArgs>? SyncComplete;
    public event EventHandler<SyncErrorEventArgs>? SyncError;
    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public GrpcSyncClient(string serverAddress = "http://localhost:50051")
    {
        _serverAddress = serverAddress;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _channel = GrpcChannel.ForAddress(_serverAddress);
            _client = new SyncEngine.SyncEngineClient(_channel);

            // Test connection
            var healthCheck = await _client.CheckHealthAsync(
                new Empty(), 
                cancellationToken: cancellationToken);

            return healthCheck.Status == "healthy";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to connect to sync engine: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_channel != null)
        {
            await _channel.ShutdownAsync();
            _channel = null;
            _client = null;
        }
    }

    public async Task<List<SyncJob>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var response = await _client.ListJobsAsync(
                new Empty(), 
                cancellationToken: cancellationToken);

            var jobs = new List<SyncJob>();
            foreach (var jobProto in response.Jobs)
            {
                jobs.Add(ConvertProtoToSyncJob(jobProto));
            }

            return jobs;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get jobs: {ex.Message}");
            throw;
        }
    }

    public async Task<SyncJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new JobIdRequest { JobId = jobId };
            var response = await _client.GetJobAsync(request, cancellationToken: cancellationToken);
            return ConvertProtoToSyncJob(response);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get job: {ex.Message}");
            throw;
        }
    }

    public async Task<SyncJob> CreateJobAsync(SyncJob job, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = ConvertSyncJobToProto(job);
            var response = await _client.CreateJobAsync(request, cancellationToken: cancellationToken);
            return ConvertProtoToSyncJob(response);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create job: {ex.Message}");
            throw;
        }
    }

    public async Task<SyncJob> UpdateJobAsync(SyncJob job, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = ConvertSyncJobToProto(job);
            var response = await _client.UpdateJobAsync(request, cancellationToken: cancellationToken);
            return ConvertProtoToSyncJob(response);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update job: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new JobIdRequest { JobId = jobId };
            var response = await _client.DeleteJobAsync(request, cancellationToken: cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete job: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> StartJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new JobIdRequest { JobId = jobId };
            var response = await _client.StartJobAsync(request, cancellationToken: cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start job: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> StopJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new JobIdRequest { JobId = jobId };
            var response = await _client.StopJobAsync(request, cancellationToken: cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop job: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> PauseJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new JobIdRequest { JobId = jobId };
            var response = await _client.PauseJobAsync(request, cancellationToken: cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to pause job: {ex.Message}");
            throw;
        }
    }

    public async Task<List<FileNode>> GetJobFilesAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new JobIdRequest { JobId = jobId };
            var response = await _client.ListJobFilesAsync(request, cancellationToken: cancellationToken);

            var files = new List<FileNode>();
            foreach (var fileProto in response.Files)
            {
                files.Add(ConvertProtoToFileNode(fileProto));
            }

            return files;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get job files: {ex.Message}");
            throw;
        }
    }

    public async IAsyncEnumerable<SyncEngine.StreamSyncProgressResponse> StreamSyncProgressAsync(
        string jobId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        var request = new JobIdRequest { JobId = jobId };
        using var call = _client.StreamSyncProgress(request, cancellationToken: cancellationToken);

        await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
        {
            SyncProgress?.Invoke(this, new SyncProgressEventArgs
            {
                JobId = jobId,
                TotalFiles = update.TotalFiles,
                SyncedFiles = update.SyncedFiles,
                FailedFiles = update.FailedFiles,
                TotalBytes = update.TotalBytes,
                TransferredBytes = update.TransferredBytes,
                CurrentFile = update.CurrentFile,
                Progress = update.Progress
            });

            yield return update;
        }
    }

    private static SyncJob ConvertProtoToSyncJob(SyncEngine.SyncJob proto)
    {
        return new SyncJob
        {
            Id = proto.Id,
            Name = proto.Name,
            SourcePath = proto.SourcePath,
            DestinationPath = proto.DestinationPath,
            Direction = (SyncDirection)proto.Direction,
            Mode = (SyncMode)proto.Mode,
            Status = (JobStatus)proto.Status,
            IsEnabled = proto.IsEnabled,
            LastSyncTime = proto.LastSyncTime.ToDateTime(),
            SyncInterval = TimeSpan.FromSeconds(proto.SyncIntervalSeconds),
            TotalFiles = proto.TotalFiles,
            SyncedFiles = proto.SyncedFiles,
            FailedFiles = proto.FailedFiles,
            TotalBytes = proto.TotalBytes,
            TransferredBytes = proto.TransferredBytes,
            Progress = proto.Progress,
            ErrorMessage = string.IsNullOrEmpty(proto.ErrorMessage) ? null : proto.ErrorMessage
        };
    }

    private static SyncEngine.SyncJob ConvertSyncJobToProto(SyncJob job)
    {
        return new SyncEngine.SyncJob
        {
            Id = job.Id,
            Name = job.Name,
            SourcePath = job.SourcePath,
            DestinationPath = job.DestinationPath,
            Direction = (SyncEngine.SyncDirection)job.Direction,
            Mode = (SyncEngine.SyncMode)job.Mode,
            Status = (SyncEngine.JobStatus)job.Status,
            IsEnabled = job.IsEnabled,
            LastSyncTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(job.LastSyncTime),
            SyncIntervalSeconds = (long)job.SyncInterval.TotalSeconds,
            TotalFiles = job.TotalFiles,
            SyncedFiles = job.SyncedFiles,
            FailedFiles = job.FailedFiles,
            TotalBytes = job.TotalBytes,
            TransferredBytes = job.TransferredBytes,
            Progress = job.Progress,
            ErrorMessage = job.ErrorMessage ?? string.Empty
        };
    }

    private static FileNode ConvertProtoToFileNode(SyncEngine.FileNode proto)
    {
        return new FileNode
        {
            Id = proto.Id,
            Name = proto.Name,
            Path = proto.Path,
            RelativePath = proto.RelativePath,
            IsDirectory = proto.IsDirectory,
            Size = proto.Size,
            ModifiedTime = proto.ModifiedTime.ToDateTime(),
            Hash = proto.Hash,
            Status = (FileSyncStatus)proto.Status,
            ConflictStatus = (FileConflictStatus)proto.ConflictStatus
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for sync progress updates
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public string JobId { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int SyncedFiles { get; set; }
    public int FailedFiles { get; set; }
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public double Progress { get; set; }
}

/// <summary>
/// Event arguments for sync completion
/// </summary>
public class SyncCompleteEventArgs : EventArgs
{
    public string JobId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event arguments for sync errors
/// </summary>
public class SyncErrorEventArgs : EventArgs
{
    public string JobId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

/// <summary>
/// Event arguments for file change notifications
/// </summary>
public class FileChangedEventArgs : EventArgs
{
    public string JobId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public FileChangeType ChangeType { get; set; }
}

/// <summary>
/// Types of file changes
/// </summary>
public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}

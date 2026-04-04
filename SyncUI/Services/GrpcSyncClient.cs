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
    private SyncEngine.SyncEngine.SyncEngineClient? _client;
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
            _client = new SyncEngine.SyncEngine.SyncEngineClient(_channel);

            // Test connection by listing jobs
            var testResponse = await _client.ListJobsAsync(
                new SyncEngine.ListJobsRequest(),
                cancellationToken: cancellationToken);

            return true;
        }
        catch (Grpc.Core.RpcException ex) when (ex.Status.StatusCode == Grpc.Core.StatusCode.Unavailable)
        {
            Debug.WriteLine($"Failed to connect to sync engine: Server is not running at {_serverAddress}");
            Debug.WriteLine($"Please start the sync-engine server first using: cd sync-engine && cargo run -- --addr 127.0.0.1:50051");
            Debug.WriteLine($"Or run the startup script: start-app.bat");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to connect to sync engine: {ex.Message}");
            Debug.WriteLine($"Server address: {_serverAddress}");
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
                new SyncEngine.ListJobsRequest(), 
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

    public async Task<SyncJob> CreateJobAsync(SyncJob job, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = ConvertSyncJobToCreateRequest(job);
            var response = await _client.CreateJobAsync(request, cancellationToken: cancellationToken);
            
            if (!response.Success)
            {
                throw new InvalidOperationException($"Failed to create job: {response.Message}");
            }

            // Get the created job by listing all jobs and finding the new one
            var jobs = await GetJobsAsync(cancellationToken);
            var createdJob = jobs.FirstOrDefault(j => j.Id == response.JobId);
            
            if (createdJob == null)
            {
                throw new InvalidOperationException("Job was created but could not be retrieved");
            }

            return createdJob;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create job: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> StartJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new SyncEngine.StartJobRequest { JobId = jobId };
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
            var request = new SyncEngine.StopJobRequest { JobId = jobId };
            var response = await _client.StopJobAsync(request, cancellationToken: cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop job: {ex.Message}");
            throw;
        }
    }

    public async Task<SyncJob?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new SyncEngine.JobStatusRequest { JobId = jobId };
            var response = await _client.JobStatusAsync(request, cancellationToken: cancellationToken);
            
            // Create a SyncJob from the status response
            var job = new SyncJob
            {
                Id = response.JobId,
                Status = (JobStatus)response.Status,
                Progress = response.Progress.Percentage,
                ErrorMessage = string.IsNullOrEmpty(response.ErrorMessage) ? null : response.ErrorMessage
            };

            return job;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get job status: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> ResolveConflictAsync(string jobId, string conflictId, SyncEngine.ConflictResolution resolution, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to sync engine");

        try
        {
            var request = new SyncEngine.ResolveConflictRequest
            {
                JobId = jobId,
                ConflictId = conflictId,
                Resolution = (SyncEngine.ConflictResolution)resolution
            };
            var response = await _client.ResolveConflictAsync(request, cancellationToken: cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve conflict: {ex.Message}");
            throw;
        }
    }

    private static SyncJob ConvertProtoToSyncJob(SyncEngine.JobSummary proto)
    {
        return new SyncJob
        {
            Id = proto.JobId,
            Name = proto.Name,
            SourcePath = proto.SourcePath,
            DestinationPath = proto.TargetPath,
            Status = (JobStatus)proto.Status,
            LastSyncTime = DateTimeOffset.FromUnixTimeSeconds(proto.CreatedAt).DateTime
        };
    }

    private static SyncEngine.CreateJobRequest ConvertSyncJobToCreateRequest(SyncJob job)
    {
        return new SyncEngine.CreateJobRequest
        {
            Name = job.Name,
            SourcePath = job.SourcePath,
            TargetPath = job.DestinationPath,
            Direction = (SyncEngine.SyncDirection)job.Direction,
            ConflictStrategy = SyncEngine.ConflictStrategy.LastWriteWins,
            EnableCompression = true,
            BlockSize = 65536 // 64KB default block size
        };
    }

    private static FileNode ConvertProtoToFileNode(SyncEngine.FileNode proto)
    {
        return new FileNode
        {
            Path = proto.Path,
            IsDirectory = proto.IsDirectory,
            Size = proto.Size,
            ModifiedTime = DateTimeOffset.FromUnixTimeSeconds(proto.ModifiedTime).DateTime,
            Hash = proto.Hash
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

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncUI.Models;

/// <summary>
/// Represents a file or directory node in the sync hierarchy
/// </summary>
public class FileNode : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _relativePath = string.Empty;
    private bool _isDirectory;
    private long _size;
    private DateTime _modifiedTime;
    private string _hash = string.Empty;
    private FileSyncStatus _status = FileSyncStatus.Unknown;
    private FileConflictStatus _conflictStatus = FileConflictStatus.None;
    private bool _isExpanded;
    private bool _isSelected;
    private List<FileNode> _children = new();

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

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => SetProperty(ref _relativePath, value);
    }

    public bool IsDirectory
    {
        get => _isDirectory;
        set => SetProperty(ref _isDirectory, value);
    }

    public long Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    public DateTime ModifiedTime
    {
        get => _modifiedTime;
        set => SetProperty(ref _modifiedTime, value);
    }

    public string Hash
    {
        get => _hash;
        set => SetProperty(ref _hash, value);
    }

    public FileSyncStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public FileConflictStatus ConflictStatus
    {
        get => _conflictStatus;
        set => SetProperty(ref _conflictStatus, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public List<FileNode> Children
    {
        get => _children;
        set => SetProperty(ref _children, value);
    }

    public string SizeFormatted => FormatFileSize(Size);
    
    public string ModifiedTimeFormatted => ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string StatusText => Status switch
    {
        FileSyncStatus.Unknown => "Unknown",
        FileSyncStatus.InSync => "In Sync",
        FileSyncStatus.New => "New",
        FileSyncStatus.Modified => "Modified",
        FileSyncStatus.Deleted => "Deleted",
        FileSyncStatus.Syncing => "Syncing...",
        FileSyncStatus.Synced => "Synced",
        FileSyncStatus.Failed => "Failed",
        FileSyncStatus.Skipped => "Skipped",
        _ => "Unknown"
    };

    public string Icon => IsDirectory ? "📁" : GetFileIcon(Name);

    public bool HasConflicts => ConflictStatus != FileConflictStatus.None;

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

    private static string FormatFileSize(long bytes)
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

    private static string GetFileIcon(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".log" or ".md" => "📄",
            ".pdf" => "📕",
            ".doc" or ".docx" => "📘",
            ".xls" or ".xlsx" => "📗",
            ".ppt" or ".pptx" => "📙",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".m4a" => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".exe" or ".msi" or ".app" => "⚙️",
            ".cs" or ".java" or ".py" or ".js" or ".ts" or ".cpp" or ".h" => "💻",
            ".html" or ".css" or ".json" or ".xml" => "🌐",
            _ => "📄"
        };
    }

    public void AddChild(FileNode child)
    {
        Children.Add(child);
        OnPropertyChanged(nameof(Children));
    }

    public void ClearChildren()
    {
        Children.Clear();
        OnPropertyChanged(nameof(Children));
    }

    public FileNode? FindChild(string id)
    {
        return Children.FirstOrDefault(c => c.Id == id);
    }

    public IEnumerable<FileNode> GetAllDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetAllDescendants())
            {
                yield return descendant;
            }
        }
    }
}

/// <summary>
/// Defines the sync status of a file
/// </summary>
public enum FileSyncStatus
{
    Unknown,
    InSync,
    New,
    Modified,
    Deleted,
    Syncing,
    Synced,
    Failed,
    Skipped
}

/// <summary>
/// Defines the conflict status of a file
/// </summary>
public enum FileConflictStatus
{
    None,
    ModifiedBothSides,
    DeletedBothSides,
    ModifiedDeleted,
    DeletedModified,
    HashMismatch,
    PermissionConflict
}

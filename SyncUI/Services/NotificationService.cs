using System.Diagnostics;

namespace SyncUI.Services;

/// <summary>
/// Service for managing notifications for Windows Desktop
/// </summary>
public class NotificationService
{
    /// <summary>
    /// Shows a local notification
    /// </summary>
    public async Task ShowNotificationAsync(string title, string message, string? jobId = null)
    {
        try
        {
            await ShowWindowsNotificationAsync(title, message, jobId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show notification: {title} - {message}: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a sync completion notification
    /// </summary>
    public async Task ShowSyncCompleteNotificationAsync(string jobName, bool success, string? errorMessage = null)
    {
        var title = success ? "Sync Complete" : "Sync Failed";
        var message = success 
            ? $"Job '{jobName}' completed successfully" 
            : $"Job '{jobName}' failed: {errorMessage}";

        await ShowNotificationAsync(title, message);
    }

    /// <summary>
    /// Shows a conflict detection notification
    /// </summary>
    public async Task ShowConflictNotificationAsync(string jobName, int conflictCount)
    {
        var title = "Conflicts Detected";
        var message = $"Job '{jobName}' has {conflictCount} conflict(s) that need resolution";
        await ShowNotificationAsync(title, message);
    }

    /// <summary>
    /// Shows an error notification
    /// </summary>
    public async Task ShowErrorNotificationAsync(string jobName, string error)
    {
        var title = "Sync Error";
        var message = $"Job '{jobName}' encountered an error: {error}";
        await ShowNotificationAsync(title, message);
    }

    /// <summary>
    /// Shows a Windows notification using Windows Forms NotifyIcon
    /// </summary>
    private async Task ShowWindowsNotificationAsync(string title, string message, string? jobId)
    {
        // This method should be called from the main UI thread
        // The actual notification will be shown using a NotifyIcon in the main form
        // For now, we'll just log it
        Debug.WriteLine($"[Notification] {title}: {message}");
        
        // TODO: Integrate with NotifyIcon in the main form
        // This will require passing a reference to the main form or using a singleton pattern
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Requests notification permissions from the user
    /// </summary>
    public async Task<bool> RequestNotificationPermissionAsync()
    {
        // Windows doesn't require explicit notification permissions for desktop apps
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Clears all notifications
    /// </summary>
    public async Task ClearAllNotificationsAsync()
    {
        // Windows doesn't have a built-in notification history for desktop apps
        // This is a no-op for Windows Forms
        await Task.CompletedTask;
    }
}

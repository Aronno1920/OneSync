using Microsoft.Extensions.Logging;

namespace SyncUI.Services;

/// <summary>
/// Service for managing notifications across platforms
/// </summary>
public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Shows a local notification
    /// </summary>
    public async Task ShowNotificationAsync(string title, string message, string? jobId = null)
    {
        try
        {
#if ANDROID
            await ShowAndroidNotificationAsync(title, message, jobId);
#elif IOS
            await ShowIOSNotificationAsync(title, message, jobId);
#elif MACCATALYST
            await ShowMacNotificationAsync(title, message, jobId);
#elif WINDOWS
            await ShowWindowsNotificationAsync(title, message, jobId);
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show notification: {Title} - {Message}", title, message);
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

#if ANDROID
    private async Task ShowAndroidNotificationAsync(string title, string message, string? jobId)
    {
        var context = Android.App.Application.Context;
        var channelId = "onesync_channel";
        var notificationId = jobId?.GetHashCode() ?? DateTime.Now.GetHashCode();

        // Create notification channel for Android O+
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
        {
            var channel = new Android.App.NotificationChannel(
                channelId,
                "OneSync Notifications",
                Android.App.NotificationImportance.High)
            {
                Description = "Notifications for OneSync sync operations"
            };

            var notificationManager = context.GetSystemService(Android.App.Context.NotificationService) 
                as Android.App.NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }

        var builder = new Android.App.Notification.Builder(context, channelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true);

        var notificationManager = context.GetSystemService(Android.App.Context.NotificationService) 
            as Android.App.NotificationManager;
        notificationManager?.Notify(notificationId, builder.Build());

        await Task.CompletedTask;
    }
#endif

#if IOS
    private async Task ShowIOSNotificationAsync(string title, string message, string? jobId)
    {
        var content = new UserNotifications.UNMutableNotificationContent
        {
            Title = title,
            Body = message,
            Sound = UserNotifications.UNNotificationSound.Default
        };

        var trigger = UserNotifications.UNTimeIntervalNotificationTrigger.CreateTrigger(0.25, false);
        var request = UserNotifications.UNNotificationRequest.FromIdentifier(
            jobId ?? Guid.NewGuid().ToString(),
            content,
            trigger);

        var center = UserNotifications.UNUserNotificationCenter.Current;
        await center.AddNotificationRequestAsync(request);
    }
#endif

#if MACCATALYST
    private async Task ShowMacNotificationAsync(string title, string message, string? jobId)
    {
        var notification = new Foundation.NSNotification
        {
            Title = title,
            InformativeText = message,
            SoundName = Foundation.NSNotificationSound.Default
        };

        Foundation.NSNotificationCenter.DefaultCenter.PostNotificationName(
            new Foundation.NSString("OneSyncNotification"),
            null,
            notification);

        await Task.CompletedTask;
    }
#endif

#if WINDOWS
    private async Task ShowWindowsNotificationAsync(string title, string message, string? jobId)
    {
        // Use Windows Toast Notifications
        var toastXml = $@"
            <toast>
                <visual>
                    <binding template='ToastGeneric'>
                        <text>{title}</text>
                        <text>{message}</text>
                    </binding>
                </visual>
            </toast>";

        var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
        xmlDoc.LoadXml(toastXml);

        var toastNotification = new Windows.UI.Notifications.ToastNotification(xmlDoc);
        var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("OneSync");
        notifier.Show(toastNotification);

        await Task.CompletedTask;
    }
#endif

    /// <summary>
    /// Requests notification permissions from the user
    /// </summary>
    public async Task<bool> RequestNotificationPermissionAsync()
    {
        try
        {
#if IOS
            var center = UserNotifications.UNUserNotificationCenter.Current;
            var (granted, error) = await center.RequestAuthorizationAsync(
                UserNotifications.UNAuthorizationOptions.Alert | 
                UserNotifications.UNAuthorizationOptions.Sound | 
                UserNotifications.UNAuthorizationOptions.Badge);

            return granted;
#elif ANDROID
            // Android notifications don't require explicit permission for normal notifications
            // Starting from Android 13 (Tiramisu), POST_NOTIFICATIONS permission is needed
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                var context = Android.App.Application.Context;
                var status = context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications);
                return status == Android.Content.PM.Permission.Granted;
            }
            return true;
#else
            return true;
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request notification permission");
            return false;
        }
    }

    /// <summary>
    /// Clears all notifications
    /// </summary>
    public async Task ClearAllNotificationsAsync()
    {
        try
        {
#if ANDROID
            var context = Android.App.Application.Context;
            var notificationManager = context.GetSystemService(Android.App.Context.NotificationService) 
                as Android.App.NotificationManager;
            notificationManager?.CancelAll();
#elif IOS
            var center = UserNotifications.UNUserNotificationCenter.Current;
            await center.RemoveAllPendingNotificationRequestsAsync();
            await center.RemoveAllDeliveredNotificationsAsync();
#elif WINDOWS
            Windows.UI.Notifications.ToastNotificationManager.History.Clear();
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear notifications");
        }

        await Task.CompletedTask;
    }
}

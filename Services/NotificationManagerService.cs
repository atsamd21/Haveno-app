﻿#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Core.App;

namespace Manta.Services;

public class NotificationManagerService : INotificationManagerService
{
    private readonly NotificationManagerCompat _compatManager;

    private const string _channelId = "default";
    private const string _channelName = "Default";
    private const string _channelDescription = "The default channel for notifications.";
    private bool _channelInitialized = false;
    private int _messageId = 0;
    private int _pendingIntentId = 0;

    public const string TitleKey = "title";
    public const string MessageKey = "message";

    public event EventHandler? NotificationReceived;

    public static NotificationManagerService? Instance { get; private set; }

    public NotificationManagerService()
    {
        CreateNotificationChannel();
        _compatManager = NotificationManagerCompat.From(Platform.AppContext);
        Instance = this;

        if (_compatManager is null)
            throw new Exception("_compatManager was null");
    }

    public void SendNotification(string title, string message, DateTime? notifyTime = null)
    {
        if (!_channelInitialized)
        {
            CreateNotificationChannel();
        }

        if (notifyTime is not null)
        {
            Intent intent = new(Platform.AppContext, typeof(AlarmHandler));

            intent.PutExtra(TitleKey, title);
            intent.PutExtra(MessageKey, message);
            intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                ? PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable
                : PendingIntentFlags.CancelCurrent;

            var pendingIntent = PendingIntent.GetBroadcast(Platform.AppContext, _pendingIntentId++, intent, pendingIntentFlags);
            if (pendingIntent is null)
                return;

            long triggerTime = GetNotifyTime(notifyTime.Value);

            var alarmManager = Platform.AppContext.GetSystemService(Context.AlarmService) as AlarmManager;
            if (alarmManager is null)
                return;

            alarmManager.Set(AlarmType.RtcWakeup, triggerTime, pendingIntent);
        }
        else
        {
            Show(title, message);
        }
    }

    public void ReceiveNotification(string title, string message)
    {
        var args = new NotificationEventArgs()
        {
            Title = title,
            Message = message,
        };

        NotificationReceived?.Invoke(null, args);
    }

    public void Show(string title, string message)
    {
        Intent intent = new(Platform.AppContext, typeof(MainActivity));

        intent.PutExtra(TitleKey, title);
        intent.PutExtra(MessageKey, message);
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(Platform.AppContext, _pendingIntentId++, intent, pendingIntentFlags);

        NotificationCompat.Builder builder = new NotificationCompat.Builder(Platform.AppContext, _channelId)
            .SetContentIntent(pendingIntent)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetLargeIcon(BitmapFactory.DecodeResource(Platform.AppContext.Resources, Resource.Drawable.haveno))
            .SetSmallIcon(Resource.Drawable.haveno);

        Notification notification = builder.Build();
        _compatManager.Notify(_messageId++, notification);
    }

    void CreateNotificationChannel()
    {
        // Create the notification channel, but only on API 26+.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channelNameJava = new Java.Lang.String(_channelName);
            var channel = new NotificationChannel(_channelId, channelNameJava, NotificationImportance.Default)
            {
                Description = _channelDescription
            };

            var manager = (NotificationManager)Platform.AppContext.GetSystemService(Context.NotificationService);
            if (manager is null)
                return;

            manager.CreateNotificationChannel(channel);
            _channelInitialized = true;
        }
    }

    long GetNotifyTime(DateTime notifyTime)
    {
        DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
        double epochDiff = (new DateTime(1970, 1, 1) - DateTime.MinValue).TotalSeconds;
        long utcAlarmTime = utcTime.AddSeconds(-epochDiff).Ticks / 10000;
        return utcAlarmTime; // milliseconds
    }
}

[BroadcastReceiver(Enabled = true, Label = "Local Notifications Broadcast Receiver")]
public class AlarmHandler : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Extras is not null)
        {
            var title = intent.GetStringExtra(NotificationManagerService.TitleKey);
            var message = intent.GetStringExtra(NotificationManagerService.MessageKey);
            if (title is null || message is null)
                return;

            NotificationManagerService manager = NotificationManagerService.Instance ?? new NotificationManagerService();
            manager.Show(title, message);
        }
    }
}

public class NotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

#endif
using Android.App;
using Android.Content;
using Android.OS;
using Manta.Singletons;
using Android.Provider;

namespace Manta.Services;

public static class AlarmUtils
{
    public static PendingIntent? GetAlarmPendingIntent(Context context)
    {
        return PendingIntent.GetBroadcast(context, 0, new Intent(context, typeof(AlarmReceiver)), PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    public static void CancelAlarm(Context context)
    {
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null)
            return;

        var pendingIntent = GetAlarmPendingIntent(context);
        if (pendingIntent is null)
            return;

        alarmManager.Cancel(pendingIntent);
    }

    public static void RequestExactAlarmPermission(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S && !AlarmManagerCanScheduleExactAlarms(context))
        {
#if !ANDROID31_0_OR_GREATER
            return;
#endif
            var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
            intent.SetData(Android.Net.Uri.Parse("package:" + context.PackageName));
            context.StartActivity(intent);
        }
    }

    public static bool AlarmManagerCanScheduleExactAlarms(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
            if (alarmManager is null)
                return false;

#if ANDROID31_0_OR_GREATER
            return alarmManager.CanScheduleExactAlarms();
#else
            return false;
#endif
        }

        return true;
    }

    public static void ScheduleExactAlarm(Context context)
    {
        // Tune this, or let user decide - higher frequency will use more battery
        var mintuesDelay = 10;

        var intent = new Intent(context, typeof(AlarmReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (pendingIntent is null)
            return;

        var triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + mintuesDelay * 60 * 1000;

        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null)
            return;

        // Use exact alarm even during Doze mode
        alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerTime, pendingIntent);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) 
            return;

        Console.WriteLine("Alarm triggered");

        var serviceProvider = IPlatformApplication.Current?.Services;
        if (serviceProvider is null)
            return;

        using var scope = serviceProvider.CreateScope();

        var notificationSingleton = scope.ServiceProvider.GetRequiredService<NotificationSingleton>();

        Task.Run(async () => await notificationSingleton.PollAsync());

        // Chaining alarms basically
        AlarmUtils.ScheduleExactAlarm(context);
    }
}

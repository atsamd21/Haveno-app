using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Manta.Models;
using System.Text;

namespace Manta.Services;

[Service(Name = $"{AppConstants.ApplicationId}.BackendService", Enabled = true, Exported = false, Permission = "android.permission.BIND_VPN_SERVICE", ForegroundServiceType = Android.Content.PM.ForegroundService.TypeDataSync)]
[IntentFilter([$"{AppConstants.ApplicationId}.ACTION_START_BACKEND", $"{AppConstants.ApplicationId}.ACTION_STOP_BACKEND"])]
public class BackendService : Service
{
    private readonly string _notificationChannelId = "BackendServiceChannel";
    private NotificationCompat.Builder? _notificationBuilder;
    private NotificationManager? _notificationManager;

    private CancellationTokenSource? _torCts;
    private CancellationTokenSource? _daemonCts;
    private TaskCompletionSource? _torReadyTCS;

    public BackendService()
    {
        
    }

#pragma warning disable CA1416
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(_notificationChannelId, "Haveno", NotificationImportance.Low)
        {
            Description = "Channel for Haveno backend",
            LockscreenVisibility = NotificationVisibility.Secret
        };

        channel.EnableLights(false);
        channel.EnableVibration(false);
        channel.SetShowBadge(false);

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }
#pragma warning restore

    private void ShowNotification()
    {
        if (string.IsNullOrEmpty(PackageName) || PackageManager is null)
            return;

        var intent = PackageManager.GetLaunchIntentForPackage(PackageName);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable);

        if (_notificationBuilder is null)
        {
            _notificationManager = (NotificationManager?)GetSystemService(Context.NotificationService);
            _notificationBuilder = new NotificationCompat.Builder(this, _notificationChannelId)
                .SetSmallIcon(Resource.Drawable.small_icon)
                .SetContentIntent(pendingIntent)
                .SetPriority(NotificationCompat.PriorityMin)
                .SetVisibility(NotificationCompat.VisibilitySecret)
                .SetCategory(Notification.CategoryService)
                .SetShowWhen(false)
                .SetSilent(true)
                .SetOngoing(true);
        }

        _notificationBuilder.MActions?.Clear();

        StartForeground(1, _notificationBuilder.Build());
    }

    public override void OnCreate()
    {
        base.OnCreate();

        if (_notificationManager is null)
        {
            _notificationManager = (NotificationManager?)GetSystemService(Context.NotificationService);
        }

        CreateNotificationChannel();
    }

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        if (intent is null)
            return StartCommandResult.RedeliverIntent;

        switch (intent.Action) 
        {
            case "ACTION_STOP_BACKEND":
                StopHavenoDaemon();
                break;
            case "ACTION_START_BACKEND":
                {
                    // TODO If running cancel
                    var password = intent.GetStringExtra("password");

                    if (password is null)
                        return StartCommandResult.RedeliverIntent;

                    ShowNotification();

                    Task.Run(() => StartBackend(password));
                }
                break;
            default: break;
        }

        return StartCommandResult.RedeliverIntent;
    }

    void UpdateProgress(string progress, bool isDone = false)
    {
        var intent = new Intent($"{AppConstants.ApplicationId}.BACKEND_PROGRESS");
        intent.PutExtra("progress", progress);
        if (isDone)
            intent.PutExtra("isDone", isDone);

        intent.SetPackage(Android.App.Application.Context.PackageName);
        SendBroadcast(intent);
    }

    private void StartBackend(string password)
    {
        _torReadyTCS = new();

        var serviceProvider = IPlatformApplication.Current?.Services;
        if (serviceProvider is null)
            throw new Exception("serviceProvider was null in StartBackend()");

        var havenoDaemonService = serviceProvider.GetRequiredService<IHavenoDaemonService>();
        var daemonPath = havenoDaemonService.GetDaemonPath();

        // Tor does not always connect successfully, need to timeout and give the user the option to restart
        _ = Task.Factory.StartNew(() =>
        {
            _torCts = new();
            using var streamReader = Proot.RunProotUbuntuCommand("tor", _torCts.Token);

            string? line;
            while ((line = streamReader.ReadLine()) is not null)
            {
#if DEBUG
                Console.WriteLine(line);
#endif
                int lastPercentage = 0;

                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == '%')
                    {
                        StringBuilder stringBuilder = new();
                        for (int j = i - 1; j > 0; j--)
                        {
                            if (line[j] > '9' || line[j] < '0')
                                break;

                            stringBuilder.Append(line[j]);
                        }

                        if (stringBuilder.Length > 0)
                        {
                            var percentage = int.Parse(stringBuilder.ToString().Reverse().ToArray());

                            if (percentage > lastPercentage)
                            {
                                lastPercentage = percentage;

                                UpdateProgress($"Tor bootstrapping: {percentage}%");

                                if (percentage == 100)
                                {
                                    _torReadyTCS.SetResult();
                                }
                            }
                        }
                    }
                }
            }
        }, TaskCreationOptions.LongRunning);

        _ = Task.Factory.StartNew(async () =>
        {
            await _torReadyTCS.Task;

            UpdateProgress("Starting daemon");

            _daemonCts = new();

            Proot.AppHome = daemonPath;

            string xmrNode;

            if (Helpers.Preferences.Get<bool>(Helpers.Preferences.UseCustomXmrNode))
            {
                xmrNode = string.Empty;
            }
            else
            {
                //xmrNode = AppConstants.Network == "XMR_MAINNET" ? "--xmrNode=http://38.105.209.54:18089" : "--xmrNode=http://45.63.8.26:38081";
                // 95.217.143.178 = rucknium.me
                xmrNode = AppConstants.Network == "XMR_MAINNET" ? "--xmrNode=http://95.217.143.178:18081" : "--xmrNode=http://45.63.8.26:38081";
            }

#if DEBUG
            var logLevel = "--logLevel=INFO";
#else
            // Have to leave this as INFO for now as we parse the output
            var logLevel = "--logLevel=INFO";
            //var logLevel = "--logLevel=OFF";
#endif

            // For some reason hostnames are bugged so have to specify monero node with ip address
            using var streamReader = Proot.RunProotUbuntuCommand("java", _daemonCts.Token, "-Xmx2G", "-jar", $"{Path.Combine(daemonPath, "daemon.jar")}", xmrNode, "--walletRpcBindPort=4000", logLevel, "--maxMemory=1200", "--disableRateLimits=true", $"--baseCurrencyNetwork={AppConstants.Network}", "--ignoreLocalXmrNode=true", "--useDevPrivilegeKeys=false", "--nodePort=9999", $"--appName={AppConstants.HavenoAppName}", $"--apiPassword={password}", "--apiPort=3201", "--passwordRequired=false", "--useNativeXmrWallet=false", "--torControlHost=127.0.0.1", "--torControlPort=9061");

            string? line;
            while ((line = streamReader.ReadLine()) is not null)
            {
#if DEBUG
                Console.WriteLine(line);
#endif
                if (line.Contains("Init wallet"))
                {
                    UpdateProgress("Initializing wallet", true);
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    public void StopHavenoDaemon()
    {
        _daemonCts?.Cancel();
        _torCts?.Cancel();

        StopSelf();
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }
}

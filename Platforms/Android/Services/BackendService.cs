using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Manta.Models;
using System.Net.Sockets;
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
                .SetSmallIcon(Resource.Drawable.small_icon)?
                .SetContentIntent(pendingIntent)?
                .SetPriority(NotificationCompat.PriorityMin)?
                .SetVisibility(NotificationCompat.VisibilitySecret)?
                .SetCategory(Notification.CategoryService)?
                .SetShowWhen(false)?
                .SetSilent(true)?
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

//        // Tor does not always connect successfully, need to timeout and give the user the option to restart
//        _ = Task.Factory.StartNew(() =>
//        {
//            _torCts = new();
//            using var streamReader = Proot.RunProotUbuntuCommand("tor", _torCts.Token);

//            string? line;
//            while ((line = streamReader.ReadLine()) is not null)
//            {
//#if DEBUG
//                Console.WriteLine(line);
//#endif
//                int lastPercentage = 0;

//                for (int i = 0; i < line.Length; i++)
//                {
//                    if (line[i] == '%')
//                    {
//                        StringBuilder stringBuilder = new();
//                        for (int j = i - 1; j > 0; j--)
//                        {
//                            if (line[j] > '9' || line[j] < '0')
//                                break;

//                            stringBuilder.Append(line[j]);
//                        }

//                        if (stringBuilder.Length > 0)
//                        {
//                            var percentage = int.Parse(stringBuilder.ToString().Reverse().ToArray());

//                            if (percentage > lastPercentage)
//                            {
//                                lastPercentage = percentage;

//                                UpdateProgress($"Tor bootstrapping: {percentage}%");

//                                if (percentage == 100)
//                                {
//                                    _torReadyTCS.SetResult();
//                                }
//                            }
//                        }
//                    }
//                }
//            }
//        }, TaskCreationOptions.LongRunning);

        _ = Task.Factory.StartNew(async () =>
        {
            //await _torReadyTCS.Task;

            UpdateProgress("Starting daemon");

            _daemonCts = new();

            Proot.AppHome = daemonPath;

            // node.monerodevs.org = 192.99.8.110
            // node2.monerodevs.org = 37.187.74.171
            // node3.monerodevs.org = 88.99.195.15

            // rucknium.me = 95.217.143.178
            // selsta2.featherwallet.net = 88.198.199.23
            // ravfx.its-a-node.org = 70.29.255.7

            string xmrNode = string.Empty;
            List<string> xmrNodes = AppConstants.Network == "XMR_MAINNET" ? ["http://192.99.8.110:18089", "http://37.187.74.171:18089", "http://88.99.195.15:18089"] : ["http://3.10.182.182:38081", "http://23.137.57.100:38089", "http://192.99.8.110:38089", "http://37.187.74.171:38089", "http://88.99.195.15:38089"];

            if (!string.IsNullOrEmpty(Helpers.Preferences.Get<string>(Helpers.Preferences.CustomXmrNode)))
            {
                xmrNode = $"--xmrNode={Helpers.Preferences.Get<string>(Helpers.Preferences.CustomXmrNode)}";
            }
            else
            {
                // Really we just need to fix the domain name issue and figure out why even though --xmrNodes is specified it still tries to sync with Haveno's default nodes
                // Might be due to which node was last successfully synced but either way something is overriding the cli argument
                using var tcpClient = new TcpClient();

                string? node = null;
                var random = new Random();

                while (xmrNodes.Count > 0)
                {
                    node = xmrNodes[random.Next(xmrNodes.Count)];

                    try
                    {
                        var split = node.Split(':');
                        tcpClient.ConnectAsync(split[1].Remove(0, 2), int.Parse(split[2])).Wait(TimeSpan.FromSeconds(2));

                        if (!tcpClient.Connected)
                        {
                            throw new Exception();
                        }

                        xmrNode = $"--xmrNode={node}";

                        break;
            }
                    catch (Exception)
                    {
                        xmrNodes.Remove(node);
                        continue;
                    }
                }
            }

#if DEBUG
            var logLevel = "--logLevel=INFO";
#else
            // Have to leave this as INFO for now as we parse the output
            var logLevel = "--logLevel=INFO";
            //var logLevel = "--logLevel=OFF";
#endif
            using var streamReader = Proot.RunProotUbuntuCommand("java", _daemonCts.Token, "-Xmx2G", "-jar", $"{Path.Combine(daemonPath, "daemon.jar")}", xmrNode, logLevel, "--maxMemory=1200", "--disableRateLimits=true", $"--baseCurrencyNetwork={AppConstants.Network}", "--ignoreLocalXmrNode=true", "--useDevPrivilegeKeys=false", "--nodePort=9999", $"--appName={AppConstants.HavenoAppName}", $"--apiPassword={password}", "--apiPort=3201", "--passwordRequired=false", "--useNativeXmrWallet=false");

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

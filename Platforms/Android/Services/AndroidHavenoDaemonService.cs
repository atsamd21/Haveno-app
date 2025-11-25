using Android.Content;
using AndroidX.Core.Content;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using Manta.Models;
using Manta.Singletons;
using System.Runtime.InteropServices;

namespace Manta.Services;

public class ProgressReceiver : BroadcastReceiver
{
    public event Action<string>? OnProgressChanged;
    public TaskCompletionSource CompletedTCS { get; } = new();

    public override void OnReceive(Context? context, Intent? intent)
    {
        var exceptionMessage = intent?.GetStringExtra("exception");
        if (exceptionMessage is not null)
        {
            //OnProgressChanged?.Invoke(exceptionMessage);
            CompletedTCS.SetException(new Exception(exceptionMessage));
            return;
        }

        var progress = intent?.GetStringExtra("progress");
        if (progress is null)
            return;

        OnProgressChanged?.Invoke(progress);

        var isDone = intent?.GetBooleanExtra("isDone", false);
        if (isDone is not null and true)
            CompletedTCS.SetResult();
    }
}

public class AndroidHavenoDaemonService : HavenoDaemonServiceBase
{
    private readonly GrpcChannelSingleton _grpcChannelSingleton;
    private readonly NotificationSingleton _notificationSingleton;

    public AndroidHavenoDaemonService(
        GrpcChannelSingleton grpcChannelSingleton, 
        IHavenoWalletService walletService, 
        IHavenoVersionService versionService, 
        IHavenoAccountService accountService,
        NotificationSingleton notificationSingleton
        ) : base( walletService, versionService, accountService, Path.Combine(Proot.HomeDir, "daemon"))
    {
        _grpcChannelSingleton = grpcChannelSingleton;
        _notificationSingleton = notificationSingleton;
    }

    // IF THIS BREAKS AND SAYS SOMETHING LIKE FILE DOES NOT EXIST ETC BUT IT DOES IT'S PROBABLY DUE TO LINE ENDINGS IN LIBPROOTWRAPPER. SO I DONT KNOW WHY IT WOULD REVERT BUT IT DOES AND YOU NEED TO CHANGE THE LINE ENDINGS TO LF
    public override async Task InstallHavenoDaemonAsync(IProgress<double> progressCb)
    {
        await Task.Run(async() =>
        {
            if (!await SecureStorageHelper.GetAsync<bool>("ubuntu-installed"))
            {
                using var ubuntuDownloadStream = await Proot.DownloadUbuntu(progressCb);
                await Proot.ExtractUbuntu(ubuntuDownloadStream, progressCb);
                await SecureStorageHelper.SetAsync("ubuntu-installed", true);
            }

            await DownloadHavenoDaemonAsync(progressCb);

            var arch = RuntimeInformation.OSArchitecture.ToString() == "X64" ? "amd64" : "arm64";

            Proot.RunProotUbuntuCommand("rm", "/bin/java");
            Proot.RunProotUbuntuCommand("ln", "-s", $"/usr/lib/jvm/java-21-openjdk-{arch}/bin/java", "/bin/java");
            Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.security", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.security");
            Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.policy");
            Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/default.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/lib/security/default.policy");
            Proot.RunProotUbuntuCommand("chmod", "+x", Path.Combine(_daemonPath, "daemon.jar"));
        });
    }

    public override async Task TryUpdateHavenoAsync(IProgress<double> progressCb)
    {
        await base.TryUpdateHavenoAsync(progressCb);
        Proot.RunProotUbuntuCommand("chmod", "+x", Path.Combine(_daemonPath, "daemon.jar"));
    }

    public override async Task<(bool, string)> GetIsDaemonInstalledAsync()
    {
        try
        {
            var output = Proot.RunProotUbuntuCommand("echo", "check");
            if (!output.Contains("check"))
                return (false, $"Proot check failed with output: {output}");

            output = Proot.RunProotUbuntuCommand("java", "--version");
            if (!output.Contains("21"))
                return (false, "Java check failed");

            var installedDaemonUrl = await GetInstalledDaemonUrlAsync();
            if (string.IsNullOrEmpty(installedDaemonUrl))
                return (false, "GetInstalledDaemonUrlAsync");

            return (true, string.Empty);
        }
        catch (Exception e)
        {
            return (false, e.ToString());
        }
    }

    public override async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default)
    {
        if (await IsHavenoDaemonRunningAsync())
        {
            return true;
        }

        await SecureStorageHelper.SetAsync("password", password);
        await SecureStorageHelper.SetAsync("host", host);

        _grpcChannelSingleton.CreateChannel(host, password);

        var receiver = new ProgressReceiver();
        receiver.OnProgressChanged += progressCb;

        var filter = new IntentFilter($"{AppConstants.ApplicationId}.BACKEND_PROGRESS");

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            Platform.AppContext.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
            Platform.AppContext.RegisterReceiver(receiver, filter);
        }

        var startBackendIntent = new Intent(Platform.AppContext, typeof(BackendService))
                        .SetAction("ACTION_START_BACKEND")
                        .PutExtra("password", password);

        ContextCompat.StartForegroundService(Platform.AppContext, startBackendIntent);

        try
        {
        await receiver.CompletedTCS.Task;
        }
        catch
        {
            receiver.OnProgressChanged -= progressCb;
            Platform.AppContext.UnregisterReceiver(receiver);
            throw;
        }

        _notificationSingleton.Start();

        receiver.OnProgressChanged -= progressCb;
        Platform.AppContext.UnregisterReceiver(receiver);

        return true;
    }

    public override Task<bool> TryStartTorAsync()
    {
        throw new NotImplementedException();
    }

    public override async Task StopHavenoDaemonAsync()
    {
        var stopBackendIntent = new Intent(Platform.AppContext, typeof(BackendService))
                        .SetAction("ACTION_STOP_BACKEND");

        Platform.AppContext.StartService(stopBackendIntent);

        await _notificationSingleton.StopNotificationListenerAsync();
    }
}

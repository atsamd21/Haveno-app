﻿using Android.Content;
using AndroidX.Core.Content;
using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using System.Runtime.InteropServices;

namespace Manta.Services;

public class ProgressReceiver : BroadcastReceiver
{
    public event Action<string>? OnProgressChanged;
    public TaskCompletionSource CompletedTCS { get; } = new();

    public override void OnReceive(Context? context, Intent? intent)
    {
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

    public AndroidHavenoDaemonService(
        GrpcChannelSingleton grpcChannelSingleton, 
        IHavenoWalletService walletService, 
        IHavenoVersionService versionService, 
        IHavenoAccountService accountService
        ) : base( walletService, versionService, accountService, Path.Combine(Proot.HomeDir, "daemon"))
    {
        _grpcChannelSingleton = grpcChannelSingleton;
    }

    public override async Task InstallHavenoDaemonAsync(IProgress<double> progressCb)
    {
        using var ubuntuDownloadStream = await Proot.DownloadUbuntu(progressCb);
        await Proot.ExtractUbuntu(ubuntuDownloadStream, progressCb);

        await DownloadHavenoDaemonAsync(progressCb);

        var arch = RuntimeInformation.OSArchitecture.ToString() == "X64" ? "amd64" : "arm64";

        Proot.RunProotUbuntuCommand("rm", "/bin/java");
        Proot.RunProotUbuntuCommand("ln", "-s", $"/usr/lib/jvm/java-21-openjdk-{arch}/bin/java", "/bin/java");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.security", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.security");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/java.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/conf/security/java.policy");
        Proot.RunProotUbuntuCommand("ln", "-s", "/etc/java-21-openjdk/security/default.policy", $"/usr/lib/jvm/java-21-openjdk-{arch}/lib/security/default.policy");
        Proot.RunProotUbuntuCommand("chmod", "+x", Path.Combine(_daemonPath, "daemon.jar"));
    }

    public override async Task TryUpdateHavenoAsync(IProgress<double> progressCb)
    {
        await base.TryUpdateHavenoAsync(progressCb);
        Proot.RunProotUbuntuCommand("chmod", "+x", Path.Combine(_daemonPath, "daemon.jar"));
    }

    public override async Task<bool> GetIsDaemonInstalledAsync()
    {
        try
        {
            var result = Proot.RunProotUbuntuCommand("echo", "check");
            if (!result.Contains("check"))
                return false;

            result = Proot.RunProotUbuntuCommand("java", "--version");
            if (!result.Contains("21"))
                return false;

            var localVersion = await GetHavenoLocalVersionAsync();
            if (string.IsNullOrEmpty(localVersion))
                return false;

            return true;
        }
        catch (Exception)
        {
            return false;
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

        var filter = new IntentFilter("com.haveno.BACKEND_PROGRESS");

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

        await receiver.CompletedTCS.Task;

        receiver.OnProgressChanged -= progressCb;
        Platform.AppContext.UnregisterReceiver(receiver);

        return true;
    }

    public override Task<bool> TryStartTorAsync()
    {
        throw new NotImplementedException();
    }

    public override Task StopHavenoDaemonAsync()
    {
        var startBackendIntent = new Intent(Platform.AppContext, typeof(BackendService))
                        .SetAction("ACTION_STOP_BACKEND");

        Platform.AppContext.StartService(startBackendIntent);

        return Task.CompletedTask;
    }
}

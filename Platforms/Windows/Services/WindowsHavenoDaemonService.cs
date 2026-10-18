using HavenoSharp.Services;
using HavenoSharp.Singletons;
using Manta.Helpers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Manta.Services;

public class WindowsHavenoDaemonService : HavenoDaemonServiceBase
{
    private readonly GrpcChannelSingleton _grpcChannelSingleton;

    public WindowsHavenoDaemonService(GrpcChannelSingleton grpcChannelSingleton,
        IHavenoWalletService walletService,
        IHavenoVersionService versionService,
        IHavenoAccountService accountService
        ) : base(walletService, versionService, accountService, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "daemon"))
    {
        _grpcChannelSingleton = grpcChannelSingleton;
    }

    public override async Task<(bool, string)> GetIsDaemonInstalledAsync()
    {
        var directories = Directory.GetDirectories(_daemonPath);
        if (directories.Length == 0)
        {
            return (false, "");
        }
        else
        {
            var havenoDirectory = directories.Where(x => x.Contains(_os, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            if (havenoDirectory is not null)
            {
                return (true, "");
            }
            else
            {
                return (false, "");
            }
        }
    }

    public override async Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default)
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        if (string.IsNullOrEmpty(currentDirectory))
            throw new Exception();

        var procesess = Process.GetProcesses();
        int i = 0;
        foreach (var p in procesess)
        {
            if (p.ProcessName.CompareTo("Manta") == 0)
            {
                i++;
            }
        }

        if (i > 1)
        {
            return true;
        }

        await SecureStorageHelper.SetAsync("password", password);
        await SecureStorageHelper.SetAsync("host", host);

        _grpcChannelSingleton.CreateChannel(host, password);

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var daemonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daemon");

        ProcessStartInfo startInfo = new()
        {
            FileName = isWindows ? Path.Combine(daemonPath, _os, "haveno-daemon.bat") : Path.Combine(daemonPath, _os, "haveno-daemon"),
            Arguments = "--baseCurrencyNetwork=XMR_STAGENET " +
                        "--useLocalhostForP2P=false " +
                        "--useDevPrivilegeKeys=false " +
                        "--nodePort=9999 " +
                        "--appName=haveno-XMR_STAGENET_user1 " +
                        $"--apiPassword={password} " +
                        "--apiPort=3201 " +
                        "--passwordRequired=false " +
                        "--disableRateLimits=true " +
                        $"--useNativeXmrWallet={AppPreferences.Get<bool>(AppPreferences.UseNativeXmrWallet).ToString().ToLowerInvariant()}",

            WorkingDirectory = currentDirectory
        };

        var process = Process.Start(startInfo);

        if (process is null)
            throw new Exception("process was null");

        process.Exited += (sender, e) =>
        {
            Console.WriteLine("Haveno daemon exited");
        };

        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("SIGINT received");

            process.StandardInput.Close();

            e.Cancel = false;
        };

        //await process.WaitForExitAsync();

        return true;
    }

    public override Task<bool> TryStartTorAsync()
    {
        throw new NotImplementedException();
    }

    public override Task StopHavenoDaemonAsync()
    {
        return Task.CompletedTask;
    }

    public override Task InstallHavenoDaemonAsync(IProgress<double> progressCb)
    {
        return Task.CompletedTask;
    }
}

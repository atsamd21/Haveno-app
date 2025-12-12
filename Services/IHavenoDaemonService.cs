namespace Manta.Services;

public interface IHavenoDaemonService
{
    Task<(bool, string)> GetIsDaemonInstalledAsync();
    Task InstallHavenoDaemonAsync(IProgress<double> progressCb);
    Task<bool> TryStartLocalHavenoDaemonAsync(string password, string host, Action<string>? progressCb = default);
    Task<bool> TryStartTorAsync();
    Task<bool> WaitHavenoDaemonInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> WaitWalletInitializedAsync(CancellationToken cancellationToken = default);
    Task<bool> IsHavenoDaemonRunningAsync(CancellationToken cancellationToken = default);
    Task StopHavenoDaemonAsync();
    Task TryUpdateHavenoAsync(IProgress<double> progressCb);
    string GetDaemonPath();
}

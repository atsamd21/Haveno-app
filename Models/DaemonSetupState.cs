namespace Manta.Models;

public enum DaemonSetupState
{
    Initial,
    InstallingDependencies,
    ExtractingRootfs,
    InstallingDaemon,
    UpdatingRootfs,
    UpdatingDaemon,
    Finished
}
using Manta.Extensions;
using Manta.Helpers;

namespace Manta.Services;

public class RootfsInstallationException : Exception
{
    public RootfsInstallationException()
    {

    }

    public RootfsInstallationException(string? message) : base(message)
    {

    }

    public RootfsInstallationException(string? message, Exception? innerException) : base(message, innerException) 
    {

    }
}

public static class RootfsInstaller
{
    private static readonly string _ubuntuDownloadUrl;

    public static string LatestRootfsVersionString { get; } = "1.0.7";
    public static Version LatestRootfsVersion { get { return Version.Parse(LatestRootfsVersionString); } }

    static RootfsInstaller()
    {
        _ubuntuDownloadUrl = $"https://github.com/atsamd21/rootfs/releases/download/v{LatestRootfsVersionString}/ubuntu-base-{RuntimeInformationExtensions.GetOsArchitectureFullName()}.tar.gz";
    }

    public static async Task InstallAsync(IProgress<double> progressCb)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = Timeout.InfiniteTimeSpan;

            using var stream = await HttpClientHelper.DownloadWithProgressAsync(
                _ubuntuDownloadUrl,
                progressCb,
                client);

            if (IsInstalled())
            {
                DeleteRootfs();
            }

            // TODO refactor this
            progressCb.Report(101f);

            if (!Directory.Exists(ProotGlobals.RootfsDir))
                Directory.CreateDirectory(ProotGlobals.RootfsDir);

            await Tar.ExtractGzAsync(stream, ProotGlobals.RootfsDir);

            // Set current installed version
            AppPreferences.Set(AppPreferences.RootfsVersion, LatestRootfsVersionString);
        }
        catch (TarExtractorException e)
        {
            Console.WriteLine(e);
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Returns null if no rootfs is installed.
    /// </summary>
    /// <returns></returns>
    public static string? GetInstalledVersionString()
    {
        return AppPreferences.Get<string>(AppPreferences.RootfsVersion);
    }

    /// <summary>
    /// Returns null if no rootfs is installed.
    /// </summary>
    /// <returns></returns>
    public static Version? GetInstalledVersion()
    {
        if (Version.TryParse(GetInstalledVersionString(), out var version))
        {
            return version;
        }

        return default;
    }

    public static bool IsInstalled()
    {
        return Directory.Exists(ProotGlobals.RootfsDir) && Directory.GetDirectories(ProotGlobals.RootfsDir).Length != 0;
    }

    private static void SetAllPermissionsRecursively(string path)
    {
        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute
            );
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        try
        {
            var directories = Directory.GetDirectories(path);

            foreach (var directory in directories)
            {
                SetAllPermissionsRecursively(directory);
            }
        }
        catch (UnauthorizedAccessException e)
        {
            Console.WriteLine(e);
        }
    }

    public static void DeleteRootfs()
    {
        SetAllPermissionsRecursively(ProotGlobals.RootfsDir);

        // TODO handle delete failure
        try
        {
            Directory.Delete(ProotGlobals.RootfsDir, true);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        AppPreferences.Set<string>(AppPreferences.RootfsVersion, null);
    }
}

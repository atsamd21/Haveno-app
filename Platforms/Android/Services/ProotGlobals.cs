using Manta.Extensions;

namespace Manta.Services;

public static class ProotGlobals
{
    public static string ProotPath { get; private set; }
    public static string RootfsDir { get; private set; }
    public static string AndroidFilesDir { get; private set; }
    public static string TmpDir { get; private set; }
    public static string HomeDir { get; private set; }

    static ProotGlobals()
    {
        ProotPath = Path.Combine(Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir!, "libprootwrapper.so");

        AndroidFilesDir = Android.App.Application.Context.FilesDir!.AbsolutePath;
        //RootfsDir = Path.Combine(AndroidFilesDir, "ubuntu");

        RootfsDir = Path.Combine(AndroidFilesDir, $"ubuntu-base-{RuntimeInformationExtensions.GetOsArchitectureFullName()}");
        Directory.CreateDirectory(RootfsDir);

        TmpDir = Path.Combine(AndroidFilesDir, "tmp");
        Directory.CreateDirectory(TmpDir);

        HomeDir = Path.Combine(AndroidFilesDir, "home");
        Directory.CreateDirectory(HomeDir);

        File.SetUnixFileMode(
            RootfsDir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute
        );

        File.SetUnixFileMode(
            TmpDir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute
        );

        File.SetUnixFileMode(
            HomeDir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute
        );
    }
}

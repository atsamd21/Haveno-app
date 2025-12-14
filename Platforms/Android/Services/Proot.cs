using Manta.Helpers;
using System.Runtime.InteropServices;
using System.Text;

namespace Manta.Services;

public static class Proot
{
    private static string _prootPath;
    private static string _filesDir;
    private static string _tmpDir;
    private static string _ubuntuTarName;

    public static string HomeDir;
    public static string AppHome = string.Empty;

    static Proot()
    {
        string architecture;
        switch (RuntimeInformation.OSArchitecture)
        {
            case Architecture.X64:
                architecture = "x86_64";
                break;
            case Architecture.Arm64:
                architecture = "arm64-v8a";
                break;
            case Architecture.Arm:
                architecture = "armeabi-v7a";
                break;
            default: throw new NotSupportedException($"Architecture \"{RuntimeInformation.OSArchitecture}\" is not supported.");
        }

        _ubuntuTarName = $"ubuntu-base-{architecture}";

        _prootPath = Path.Combine(Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir!, "libprootwrapper.so");
        _filesDir = Android.App.Application.Context.FilesDir!.AbsolutePath;
        _tmpDir = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "tmp");
        HomeDir = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "home");

        Directory.CreateDirectory(_tmpDir);
        SetFullPermissions(_tmpDir);
        Directory.CreateDirectory(HomeDir);
        SetFullPermissions(HomeDir);
    }

    private static void SetFullPermissions(string path)
    {
        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static async Task<Stream> DownloadUbuntu(IProgress<double> progressCb)
    {
        using var client = new HttpClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        return await HttpClientHelper.DownloadWithProgressAsync($"https://github.com/atsamd21/rootfs/releases/download/v1.0.6/{_ubuntuTarName}.tar.gz", progressCb, client);
    }

    public static string RunProotUbuntuCommand(string command, params string[] arguments)
    {
        var args = new string[]
        {
            _prootPath,
            "-R", ProotGlobals.RootfsDir,
            "--link2symlink",
            "-0",
            "-b", $"{_tmpDir}:/tmp",
            "-b", $"{HomeDir}:/home",
            "-w", "/",
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/sys",
            "-b", "/apex:/apex",
            "-b", "/system",
            $"{command}",
            // Exit?
        }.Concat(arguments).ToArray();

        var processBuilder = new Java.Lang.ProcessBuilder()
            .Command(args)?
            .RedirectErrorStream(true);

        var env = processBuilder?.Environment();
        env?.Remove("TMPDIR");
        env?.Remove("PROOT_TMP_DIR");

        env?.Add("TMPDIR", _tmpDir);
        env?.Add("PROOT_TMP_DIR", _tmpDir);

        env?.Remove("PATH");
        env?.Add("PATH", "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin");

        var process = processBuilder?.Start();
        if (process is null || process.InputStream is null)
            throw new Exception("process is null or process.InputStream is null");

        using var streamReader = new StreamReader(process.InputStream);

        // Might not be such a good idea with long running processes
        var stringBuilder = new StringBuilder();
        string? line;
        while ((line = streamReader.ReadLine()) is not null)
        {
            Console.WriteLine(line);
            stringBuilder.AppendLine(line);
        }

        process.WaitFor();

        return stringBuilder.ToString();
    }

    public static StreamReader RunProotUbuntuCommand(string command, CancellationToken cancellationToken, params string[] arguments)
    {
        var args = new string[]
        {
            _prootPath,
            "-R", ProotGlobals.RootfsDir,
            "--link2symlink",
            "-0",
            "-b", $"{_tmpDir}:/tmp",
            "-b", $"{HomeDir}:/home",
            "-w", "/",
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/sys",
            "-b", "/apex:/apex",
            "-b", "/system",
            //"-b", "/system/lib64:/android-lib64",
            //"-b", "/system/lib:/android-lib",
            $"{command}",
            // Exit?
        }.Concat(arguments).ToArray();

        var processBuilder = new Java.Lang.ProcessBuilder()
            .Command(args)?
            .RedirectErrorStream(true);

        var env = processBuilder?.Environment();
        env?.Remove("TMPDIR");
        env?.Remove("PROOT_TMP_DIR");

        env?.Add("TMPDIR", _tmpDir);
        env?.Add("PROOT_TMP_DIR", _tmpDir);
        env?.Add("APP_HOME", AppHome);

        env?.Remove("PATH");
        env?.Add("PATH", "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin");

        var process = processBuilder?.Start();
        if (process is null || process.InputStream is null)
            throw new Exception("process is null or process.InputStream is null");

        var streamReader = new StreamReader(process.InputStream);

        cancellationToken.Register(() =>
        {
            var pidField = process.Class.GetDeclaredField("pid");
            pidField.Accessible = true;
            int pid = pidField.GetInt(process);

            Android.OS.Process.SendSignal(pid, Android.OS.Signal.Quit);

            process.WaitFor();
            process.Dispose();

            streamReader.Close();
        });

        return streamReader;
    }
}

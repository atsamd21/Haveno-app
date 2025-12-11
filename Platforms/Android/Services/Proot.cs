using Manta.Helpers;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Manta.Services;

public static class Proot
{
    private static string _prootPath;
    private static string _rootfsDir;
    private static string _filesDir;
    private static string _tmpDir;
    private static string _ubuntuTarName;

    public static string HomeDir;
    public static string AppHome = string.Empty;

    static Proot()
    {
        _ubuntuTarName = "ubuntu-base-" + (RuntimeInformation.OSArchitecture.ToString() == "X64" ? "x86_64" : "arm64-v8a");

        _prootPath = Path.Combine(Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir!, "libprootwrapper.so");
        _filesDir = Android.App.Application.Context.FilesDir!.AbsolutePath;
        _rootfsDir = Path.Combine(_filesDir, _ubuntuTarName);
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

    private static async Task CreateFileWithPermissions(string path, Stream data)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

        await data.CopyToAsync(fs);

        SetFullPermissions(path);
    }

    public static async Task<Stream> DownloadUbuntu(IProgress<double> progressCb)
    {
        using var client = new HttpClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        return await HttpClientHelper.DownloadWithProgressAsync($"https://github.com/atsamd21/rootfs/releases/download/v1.0.6/{_ubuntuTarName}.tar.gz", progressCb, client);
    }

    public static async Task ExtractUbuntu(Stream ubuntuDownloadStream, IProgress<double> progressCb)
    {
        progressCb.Report(101f);

        File.SetUnixFileMode(
            _tmpDir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute
        );

        if (File.GetUnixFileMode(_tmpDir) != (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute))
        {
            throw new Exception("Could not set file permissions");
        }

        // Can be problematic due to symlinks
        if (Directory.Exists(_rootfsDir))
            Directory.Delete(_rootfsDir, true);

        Directory.CreateDirectory(_rootfsDir);
        SetFullPermissions(_rootfsDir);

        using var gzipStream = new GZipStream(ubuntuDownloadStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync()) is not null)
        {
            string destPath = Path.Combine(_rootfsDir, entry.Name);
            string? parentDir = Path.GetDirectoryName(destPath);

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(destPath);
                SetFullPermissions(destPath);
                continue;
            }
            else if (entry.EntryType == TarEntryType.SymbolicLink)
            {
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                    SetFullPermissions(parentDir);
                }

                File.CreateSymbolicLink(destPath, entry.LinkName);
            }
            else
            {
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                    SetFullPermissions(parentDir);
                }

                if (entry.DataStream is not null)
                {
                    await CreateFileWithPermissions(destPath, entry.DataStream);
                }
            }
        }
    }

    public static string RunProotUbuntuCommand(string command, params string[] arguments)
    {
        var args = new string[]
        {
            _prootPath,
            "-R", _rootfsDir,
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
            "-R", _rootfsDir,
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
            $"/usr/bin/{command}",
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

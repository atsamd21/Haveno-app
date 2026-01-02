using System.Text;

namespace Manta.Services;

public static class Proot
{
    //"-b", "/system/lib64:/android-lib64",
    //"-b", "/system/lib:/android-lib",
    private static Java.Lang.Process? CreateProotProcess(string command, params string[] arguments)
    {
        var args = new string[]
        {
            ProotGlobals.ProotPath,
            "-R", ProotGlobals.RootfsDir,
            "--kill-on-exit",
            "--link2symlink",
            "-0",
            "-b", $"{ProotGlobals.TmpDir}:/tmp",
            "-b", $"{ProotGlobals.HomeDir}:/home",
            "-b", "/dev",
            "-b", "/proc",
            "-b", "/sys",
            "-b", "/apex:/apex",
            "-b", "/system",
            $"{command}",
        }.Concat(arguments).ToArray();

        var processBuilder = new Java.Lang.ProcessBuilder()
            .Command(args)?
            .RedirectErrorStream(true);

        var env = processBuilder?.Environment();
        env?.Remove("TMPDIR");
        env?.Remove("PROOT_TMP_DIR");

        env?.Add("TMPDIR", ProotGlobals.TmpDir);
        env?.Add("PROOT_TMP_DIR", ProotGlobals.TmpDir);

        env?.Remove("PATH");
        env?.Add("PATH", "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin");

        env?.Add("DEBIAN_FRONTEND", "noninteractive");
        env?.Add("DEBCONF_NONINTERACTIVE_SEEN", "true");
        env?.Add("LC_ALL", "C");
        env?.Add("LANGUAGE", "C");

        env?.Remove("LANG");
        env?.Add("LANG", "C");

        return processBuilder?.Start();
    }

    public static string RunProotUbuntuCommand(string command, params string[] arguments)
    {
        var process = CreateProotProcess(command, arguments);
        if (process is null || process.InputStream is null)
            throw new Exception("process is null or process.InputStream is null.");

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
        var process = CreateProotProcess(command, arguments);
        if (process is null || process.InputStream is null)
            throw new Exception("process is null or process.InputStream is null.");

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

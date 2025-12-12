//using SharpCompress.Compressors.BZip2;
using System.Formats.Tar;
using System.IO.Compression;

namespace Manta.Helpers;

public class Tar
{
    //public static async Task ExtractBz2Async(Stream bz2TarStream, string installPath)
    //{
    //    using var bzip2Stream = new BZip2Stream(bz2TarStream, SharpCompress.Compressors.CompressionMode.Decompress, false);
    //    await ExtractAsync(bzip2Stream, installPath);
    //}

    public static async Task ExtractGzAsync(Stream gzTarStream, string installPath)
    {
        using var gzipStream = new GZipStream(gzTarStream, CompressionMode.Decompress);
        await ExtractAsync(gzipStream, installPath);
    }

    public static async Task ExtractAsync(Stream tarStream, string installPath)
    {
        using var tarReader = new TarReader(tarStream);

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync()) is not null)
        {
            string destinationPath = Path.Combine(installPath, entry.Name);
            string? parentDirectory = Path.GetDirectoryName(destinationPath);

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(destinationPath);
                File.SetUnixFileMode(destinationPath, entry.Mode);
            }
            else if (entry.EntryType == TarEntryType.SymbolicLink)
            {
                if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                    File.SetUnixFileMode(parentDirectory, entry.Mode);
                }

                File.CreateSymbolicLink(destinationPath, entry.LinkName);
            }
            else
            {
                if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                    File.SetUnixFileMode(parentDirectory, entry.Mode);
                }

                if (entry.DataStream is not null)
                {
                    using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                    await entry.DataStream.CopyToAsync(fs);

                    File.SetUnixFileMode(destinationPath, entry.Mode);
                }
            }
        }
    }
}

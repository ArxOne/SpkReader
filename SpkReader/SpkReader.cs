using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ArxOne.Synology;

public class SpkReader
{
    private readonly Stream _inputStream;

    public SpkReader(Stream inputStream)
    {
        _inputStream = inputStream;
    }

    public (IReadOnlyDictionary<string, object>? Info, IReadOnlyDictionary<string, byte[]> Icons, IReadOnlyList<string>? Files) Read(bool readInfo = true, bool readFiles = true, bool readIcons = true)
    {
        IReadOnlyDictionary<string, object>? info = null;
        IReadOnlyList<string>? files = null;
        var icons = new Dictionary<string, byte[]>(StringComparer.InvariantCultureIgnoreCase);

        bool HasInfo() => info is not null || !readInfo;
        bool HasFiles() => files is not null || !readFiles;

        using var tarReader = new TarReader(_inputStream);
        for (; ; )
        {
            var tarEntry = tarReader.GetNextEntry();
            if (tarEntry is null)
                break;
            if (tarEntry.DataStream is null)
                continue;

            switch (tarEntry.Name)
            {
                case "INFO":
                    info = ReadInfo(tarEntry.DataStream);
                    break;
                case "package.tgz":
                    files = ReadFiles(tarEntry.DataStream).ToImmutableArray();
                    break;
                default:
                    GetPackageIcon(readIcons, tarEntry, icons);
                    break;
            }

            if (HasInfo() && HasFiles() && !readIcons)
                return (info, icons, files);
        }

        CheckErrors(HasInfo, HasFiles);
        return (info, icons, files);
    }

    private void GetPackageIcon(bool readIcons, TarEntry tarEntry, IDictionary<string, byte[]> icons)
    {
        if (readIcons && tarEntry.Name.StartsWith("PACKAGE_ICON", StringComparison.InvariantCultureIgnoreCase) && tarEntry.Name.EndsWith(".PNG", StringComparison.InvariantCultureIgnoreCase))
            icons[tarEntry.Name] = GetData(tarEntry.DataStream!);
    }

    private static void CheckErrors(Func<bool> hasInfo, Func<bool> hasFiles)
    {
        if (!hasInfo())
            throw new FormatException("No INFO found");
        if (!hasFiles())
            throw new FormatException("No package.tgz found");
    }

    private byte[] GetData(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static IReadOnlyDictionary<string, object> ReadInfo(Stream stream)
    {
        var keyValues = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
        using var textReader = new StreamReader(stream, Encoding.UTF8);
        for (; ; )
        {
            var line = textReader.ReadLine();
            if (line is null)
                break;
            var equals = line.IndexOf('=');
            if (equals < 0)
                continue;
            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (value.StartsWith('"') && value.EndsWith('"'))
                keyValues[key] = value[1..^1];
            else if (int.TryParse(value, out var longValue))
                keyValues[key] = longValue;
            else
                throw new FormatException($"Unknown line format for {line}");
        }
        return keyValues;
    }

    private static IEnumerable<string> ReadFiles(Stream stream)
    {
        using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);
        for (; ; )
        {
            var tarEntry = tarReader.GetNextEntry();
            if (tarEntry is null)
                break;

            yield return tarEntry.Name;
        }
    }

    public static (IReadOnlyDictionary<string, object>? Info, IReadOnlyDictionary<string, byte[]> Icons) ReadPackageInfo(Stream spkStream)
    {
        var spkReader = new SpkReader(spkStream);
        var (info, icons, _) = spkReader.Read(readFiles: false);
        return (info, icons);
    }
}

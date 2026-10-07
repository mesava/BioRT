using System.IO.Compression;

namespace BioRT.IO.Dicom;

public sealed record ArchiveBundleEntry(
    string Name,
    byte[] Bytes);

/// <summary>
/// Expands a commissioning/input ZIP entirely in memory.
///
/// The reader never writes archive entries to disk. This avoids path traversal
/// concerns and preserves BioRT's local-browser privacy model.
///
/// Only .dcm and .json entries are returned. Limits are enforced on both entry
/// count and cumulative uncompressed size to reduce browser zip-bomb risk.
/// </summary>
public static class ArchiveBundleReader
{
    public static async Task<IReadOnlyList<ArchiveBundleEntry>> ReadAsync(
        Stream zipStream,
        int maxEntries = 100,
        long maxExpandedBytes = 512L * 1024L * 1024L,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipStream);

        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        if (maxExpandedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExpandedBytes));

        using var archive =
            new ZipArchive(
                zipStream,
                ZipArchiveMode.Read,
                leaveOpen: true);

        ZipArchiveEntry[] supported =
            archive.Entries
                .Where(entry =>
                    !string.IsNullOrEmpty(entry.Name) &&
                    (entry.Name.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase) ||
                     entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        if (supported.Length == 0)
        {
            throw new InvalidOperationException(
                "ZIP does not contain any .dcm or .json files.");
        }

        if (supported.Length > maxEntries)
        {
            throw new InvalidOperationException(
                $"ZIP contains {supported.Length} supported files; limit is {maxEntries}.");
        }

        long declaredTotal = 0;

        foreach (ZipArchiveEntry entry in supported)
        {
            if (entry.Length < 0)
                throw new InvalidOperationException(
                    $"ZIP entry has an invalid length: '{entry.FullName}'.");

            checked
            {
                declaredTotal += entry.Length;
            }

            if (declaredTotal > maxExpandedBytes)
            {
                throw new InvalidOperationException(
                    $"ZIP expanded size exceeds the configured limit of {maxExpandedBytes} bytes.");
            }
        }

        var result =
            new List<ArchiveBundleEntry>(
                supported.Length);

        long actualTotal = 0;

        foreach (ZipArchiveEntry entry in supported)
        {
            await using Stream source =
                entry.Open();

            using var target =
                entry.Length > 0 && entry.Length <= int.MaxValue
                    ? new MemoryStream((int)entry.Length)
                    : new MemoryStream();

            await source.CopyToAsync(
                target,
                cancellationToken);

            checked
            {
                actualTotal += target.Length;
            }

            if (actualTotal > maxExpandedBytes)
            {
                throw new InvalidOperationException(
                    $"ZIP expanded size exceeds the configured limit of {maxExpandedBytes} bytes.");
            }

            result.Add(
                new ArchiveBundleEntry(
                    entry.FullName.Replace('\\', '/'),
                    target.ToArray()));
        }

        return result;
    }

    public static DicomInputFile ToDicomInputFile(
        ArchiveBundleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new DicomInputFile
        {
            Name = entry.Name,
            OpenReadAsync = _ =>
                ValueTask.FromResult<Stream>(
                    new MemoryStream(
                        entry.Bytes,
                        writable: false))
        };
    }
}

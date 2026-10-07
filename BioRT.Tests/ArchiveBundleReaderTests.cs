using System.IO.Compression;
using BioRT.IO.Dicom;
using Xunit;

namespace BioRT.Tests;

public class ArchiveBundleReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsOnlyDicomAndJsonEntries()
    {
        await using MemoryStream zip =
            CreateZip(
                ("case/RTPLAN.dcm", new byte[] { 1, 2, 3 }),
                ("case/RTDOSE.dcm", new byte[] { 4, 5 }),
                ("case/criteria.json", "{}"u8.ToArray()),
                ("case/readme.txt", "ignore"u8.ToArray()));

        IReadOnlyList<ArchiveBundleEntry> entries =
            await ArchiveBundleReader.ReadAsync(zip);

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, x => x.Name == "case/RTPLAN.dcm");
        Assert.Contains(entries, x => x.Name == "case/RTDOSE.dcm");
        Assert.Contains(entries, x => x.Name == "case/criteria.json");
        Assert.DoesNotContain(entries, x => x.Name.EndsWith(".txt"));
    }

    [Fact]
    public async Task ReadAsync_RejectsArchiveWithoutSupportedFiles()
    {
        await using MemoryStream zip =
            CreateZip(
                ("readme.txt", "nothing"u8.ToArray()));

        InvalidOperationException ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await ArchiveBundleReader.ReadAsync(zip));

        Assert.Contains(
            "does not contain",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_RejectsTooManySupportedEntries()
    {
        await using MemoryStream zip =
            CreateZip(
                ("a.dcm", new byte[] { 1 }),
                ("b.dcm", new byte[] { 2 }));

        InvalidOperationException ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await ArchiveBundleReader.ReadAsync(
                        zip,
                        maxEntries: 1,
                        maxExpandedBytes: 1024));

        Assert.Contains(
            "limit",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_RejectsExpandedSizeOverLimit()
    {
        await using MemoryStream zip =
            CreateZip(
                ("dose.dcm", new byte[32]));

        InvalidOperationException ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await ArchiveBundleReader.ReadAsync(
                        zip,
                        maxEntries: 10,
                        maxExpandedBytes: 16));

        Assert.Contains(
            "expanded size",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToDicomInputFile_ReopensIndependentMemoryStream()
    {
        var entry =
            new ArchiveBundleEntry(
                "RTSTRUCT.dcm",
                new byte[] { 7, 8, 9 });

        DicomInputFile input =
            ArchiveBundleReader.ToDicomInputFile(entry);

        await using Stream first =
            await input.OpenReadAsync(CancellationToken.None);

        await using Stream second =
            await input.OpenReadAsync(CancellationToken.None);

        Assert.NotSame(first, second);

        Assert.Equal(
            new byte[] { 7, 8, 9 },
            ReadAll(first));

        Assert.Equal(
            new byte[] { 7, 8, 9 },
            ReadAll(second));
    }

    private static MemoryStream CreateZip(
        params (string Name, byte[] Bytes)[] entries)
    {
        var stream =
            new MemoryStream();

        using (var archive =
               new ZipArchive(
                   stream,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            foreach ((string name, byte[] bytes) in entries)
            {
                ZipArchiveEntry entry =
                    archive.CreateEntry(
                        name,
                        CompressionLevel.Fastest);

                using Stream target =
                    entry.Open();

                target.Write(
                    bytes,
                    0,
                    bytes.Length);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static byte[] ReadAll(
        Stream stream)
    {
        using var target =
            new MemoryStream();

        stream.CopyTo(target);
        return target.ToArray();
    }
}

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
    public async Task ReadAsync_WorksWithAsyncOnlyBrowserLikeStream()
    {
        await using MemoryStream zip =
            CreateZip(
                ("RTPLAN.dcm", new byte[] { 1, 2, 3 }),
                ("criteria.json", "{}"u8.ToArray()));

        await using var browserLike =
            new AsyncOnlyReadStream(
                zip.ToArray());

        IReadOnlyList<ArchiveBundleEntry> entries =
            await ArchiveBundleReader.ReadAsync(browserLike);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, x => x.Name == "RTPLAN.dcm");
        Assert.Contains(entries, x => x.Name == "criteria.json");
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

    private sealed class AsyncOnlyReadStream : Stream
    {
        private readonly MemoryStream _inner;

        public AsyncOnlyReadStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes, writable: false);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
            => throw new NotSupportedException(
                "Synchronous reads are not supported.");

        public override int Read(
            Span<byte> buffer)
            => throw new NotSupportedException(
                "Synchronous reads are not supported.");

        public override int ReadByte()
            => throw new NotSupportedException(
                "Synchronous reads are not supported.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => _inner.ReadAsync(
                buffer,
                cancellationToken);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => _inner.ReadAsync(
                buffer,
                offset,
                count,
                cancellationToken);

        public override long Seek(
            long offset,
            SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            await base.DisposeAsync();
        }
    }

}

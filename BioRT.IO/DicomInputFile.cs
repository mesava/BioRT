namespace BioRT.IO.Dicom;

public sealed class DicomInputFile
{
    public required string Name { get; init; }

    public required Func<CancellationToken, ValueTask<Stream>> OpenReadAsync { get; init; }

    public static DicomInputFile FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        return new DicomInputFile
        {
            Name = Path.GetFileName(path),
            OpenReadAsync = _ =>
                ValueTask.FromResult<Stream>(
                    new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 64 * 1024,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan))
        };
    }
}

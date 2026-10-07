using BioRT.IO.Dicom;
using Microsoft.AspNetCore.Components.Forms;

namespace BioRT.Web.Services;

public static class BrowserDicomInputFileFactory
{
    public static DicomInputFile Create(
        IBrowserFile file,
        long maxAllowedSize)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (maxAllowedSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAllowedSize));

        return new DicomInputFile
        {
            Name = file.Name,
            OpenReadAsync = cancellationToken =>
                ValueTask.FromResult<Stream>(
                    file.OpenReadStream(
                        maxAllowedSize,
                        cancellationToken))
        };
    }
}

namespace MorrusPOS.Api.Files;

public static class AtomicFileWriter
{
    public static async Task WriteAsync(Stream source, string destinationPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new ArgumentException("Destination path must include a directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{destinationPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }
}

using MorrusPOS.Api.Files;

namespace MorrusPOS.IntegrationTests;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"morrus-atomic-{Guid.NewGuid():N}");

    [Fact]
    public async Task WriteAsync_PublishesCompleteFileWithoutTemporaryFiles()
    {
        var destination = Path.Combine(_directory, "nested", "image.jpg");
        await using var source = new MemoryStream("complete"u8.ToArray());

        await AtomicFileWriter.WriteAsync(source, destination, CancellationToken.None);

        Assert.Equal("complete", await File.ReadAllTextAsync(destination));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp-*"));
    }

    [Fact]
    public async Task WriteAsync_RemovesPartialFileWhenSourceFails()
    {
        var destination = Path.Combine(_directory, "image.jpg");
        await using var source = new FailingStream();

        await Assert.ThrowsAsync<IOException>(() =>
            AtomicFileWriter.WriteAsync(source, destination, CancellationToken.None));

        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class FailingStream : MemoryStream
    {
        public FailingStream() : base("partial"u8.ToArray()) { }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new IOException("simulated read failure");
    }
}

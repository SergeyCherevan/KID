using System.IO;
using System.Net;
using System.Net.Http;

namespace KID.Tests.Library;

public sealed class MusicDownloadTests
{
    [Theory]
    [InlineData(0, null, 3L)]
    [InlineData(2, 2L, 3L)]
    [InlineData(3, 3L, 3L)]
    [InlineData(3, null, 3L)]
    [InlineData(250_001, null, null)]
    public async Task Download_WritesExactContentInBoundedChunks(int length, long? advertisedLength, long? limit)
    {
        var bytes = Enumerable.Range(0, length).Select(static index => (byte)(index % 251)).ToArray();
        var input = new TestReadStream(bytes);
        using var client = CreateClient(input, advertisedLength);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            await runtime.DownloadToFileAsync("https://example.test/audio", path, limit,
                TestContext.Current.CancellationToken);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
            Assert.InRange(input.LargestReadRequest, 1, 81920);
            if (length > 81920)
                Assert.True(input.ReadCount > 1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task HeaderAboveLimit_RejectsBeforeReadingOrWriting()
    {
        var input = new TestReadStream([1, 2, 3, 4]);
        using var client = CreateClient(input, 4);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => runtime.DownloadToFileAsync(
                "https://example.test/audio", path, 3, TestContext.Current.CancellationToken));
            Assert.Equal(0, input.ReadCount);
            Assert.Empty(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1L)]
    public async Task BodyAboveLimit_RejectsRegardlessOfAdvertisedLength(long? advertisedLength)
    {
        var input = new TestReadStream([1, 2, 3, 4]) { ChunkSize = 2 };
        using var client = CreateClient(input, advertisedLength);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => runtime.DownloadToFileAsync(
                "https://example.test/audio", path, 3, TestContext.Current.CancellationToken));
            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CancellationDuringBody_ClosesStreamsAndLeavesPartialFileRemovable()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var input = new TestReadStream([1, 2, 3, 4]) { ChunkSize = 2, BlockAfter = 2 };
        using var client = CreateClient(input, null);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            var download = runtime.DownloadToFileAsync("https://example.test/audio", path, null, stop.Token);
            await input.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await stop.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await download.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            File.Delete(path);
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadFailure_ClosesStreamsAndPreservesPartialFileForCallerCleanup()
    {
        var input = new TestReadStream([1, 2, 3, 4]) { ChunkSize = 2, FailAfter = 2 };
        using var client = CreateClient(input, null);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            await Assert.ThrowsAsync<IOException>(() => runtime.DownloadToFileAsync(
                "https://example.test/audio", path, null, TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task FileOpenFailure_ClosesResponseStream()
    {
        var input = new TestReadStream([1]);
        using var client = CreateClient(input, null);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            await Assert.ThrowsAsync<IOException>(() => runtime.DownloadToFileAsync(
                "https://example.test/audio", path, null, TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
            Assert.Equal(0, input.ReadCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task HttpFailure_DoesNotReadBodyAndClosesResponse()
    {
        var input = new TestReadStream([1]);
        using var client = CreateClient(input, null, HttpStatusCode.NotFound);
        var runtime = new DefaultMusicRuntime(client);
        var path = runtime.CreateTemporaryPath();
        try
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => runtime.DownloadToFileAsync(
                "https://example.test/audio", path, null, TestContext.Current.CancellationToken));
            Assert.True(input.Disposed);
            Assert.Equal(0, input.ReadCount);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task InvalidLimit_IsRejectedBeforeRequest(long limit)
    {
        using var handler = new ResponseHandler(() => throw new InvalidOperationException("Unexpected request."));
        using var client = new HttpClient(handler);
        var runtime = new DefaultMusicRuntime(client);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runtime.DownloadToFileAsync(
            "https://example.test/audio", "unused", limit, TestContext.Current.CancellationToken));
        Assert.Equal("maxBytes", error.ParamName);
    }

    [Fact]
    public async Task CancellationBeforeRequest_DoesNotAccessNetworkOrFile()
    {
        using var handler = new ResponseHandler(() => throw new InvalidOperationException("Unexpected request."));
        using var client = new HttpClient(handler);
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DefaultMusicRuntime(client)
            .DownloadToFileAsync("https://example.test/audio", "unused", null, stop.Token));
    }

    private static HttpClient CreateClient(Stream input, long? length, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new ResponseHandler(() =>
        {
            var content = new StreamContent(input);
            content.Headers.ContentLength = length;
            return new HttpResponseMessage(status) { Content = content };
        }));

    private sealed class ResponseHandler(Func<HttpResponseMessage> createResponse) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(createResponse());
        }
    }

    private sealed class TestReadStream(byte[] bytes) : Stream
    {
        private int position;
        internal int ChunkSize { get; init; } = int.MaxValue;
        internal int? BlockAfter { get; init; }
        internal int? FailAfter { get; init; }
        internal bool Disposed { get; private set; }
        internal int ReadCount { get; private set; }
        internal int LargestReadRequest { get; private set; }
        internal TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BlockAfter is int blockedAt && position >= blockedAt)
            {
                Blocked.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (FailAfter is int failedAt && position >= failedAt)
                throw new IOException("Read failed.");
            ReadCount++;
            LargestReadRequest = Math.Max(LargestReadRequest, buffer.Length);
            var count = Math.Min(Math.Min(buffer.Length, ChunkSize), bytes.Length - position);
            bytes.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}

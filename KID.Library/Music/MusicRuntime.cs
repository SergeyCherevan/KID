using System.Net.Http;
using System.IO;
using NAudio.Wave;

namespace KID;

/// <summary>
/// Изолирует NAudio, сеть и файловую систему от lifecycle-логики Music.
/// Production использует <see cref="DefaultMusicRuntime"/>, а тесты подставляют
/// детерминированные ресурсы без звуковой карты и внешней сети.
/// </summary>
internal interface IMusicRuntime
{
    IMusicOutput CreateOutput();
    IMusicFileSource OpenFile(string path);
    Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken);
    string CreateTemporaryPath();
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken);
    bool FileExists(string path);
    void DeleteFile(string path);
}

/// <summary>Минимальный управляемый Music-контракт над устройством вывода NAudio.</summary>
internal interface IMusicOutput : IDisposable
{
    PlaybackState PlaybackState { get; }
    void Init(ISampleProvider sampleProvider);
    void Play();
    void Pause();
    void Stop();
}

/// <summary>Управляемый аудиоисточник файла с позицией и громкостью.</summary>
internal interface IMusicFileSource : IDisposable
{
    ISampleProvider SampleProvider { get; }
    TimeSpan CurrentTime { get; set; }
    TimeSpan TotalTime { get; }
    long Position { get; set; }
    float Volume { get; set; }
}

internal sealed class DefaultMusicRuntime : IMusicRuntime
{
    private static readonly HttpClient HttpClient = new();

    internal static DefaultMusicRuntime Instance { get; } = new();

    private DefaultMusicRuntime()
    {
    }

    public IMusicOutput CreateOutput() => new NAudioMusicOutput();

    public IMusicFileSource OpenFile(string path) => new NAudioMusicFileSource(path);

    public async Task<byte[]> DownloadBytesAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"kid-audio-{Guid.NewGuid():N}.tmp");

    public Task WriteAllBytesAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken) =>
        File.WriteAllBytesAsync(path, content, cancellationToken);

    public bool FileExists(string path) => File.Exists(path);

    public void DeleteFile(string path) => File.Delete(path);
}

internal sealed class NAudioMusicOutput : IMusicOutput
{
    private readonly WaveOut output = new();

    public PlaybackState PlaybackState => output.PlaybackState;

    public void Init(ISampleProvider sampleProvider) => output.Init(sampleProvider);

    public void Play() => output.Play();

    public void Pause() => output.Pause();

    public void Stop() => output.Stop();

    public void Dispose() => output.Dispose();
}

internal sealed class NAudioMusicFileSource : IMusicFileSource
{
    private readonly AudioFileReader reader;

    internal NAudioMusicFileSource(string path)
    {
        reader = new AudioFileReader(path);
    }

    public ISampleProvider SampleProvider => reader;
    public TimeSpan CurrentTime { get => reader.CurrentTime; set => reader.CurrentTime = value; }
    public TimeSpan TotalTime => reader.TotalTime;
    public long Position { get => reader.Position; set => reader.Position = value; }
    public float Volume { get => reader.Volume; set => reader.Volume = value; }

    public void Dispose() => reader.Dispose();
}

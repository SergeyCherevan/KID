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
    Task DownloadToFileAsync(string url, string path, long? maxBytes, CancellationToken cancellationToken);
    /// <summary>Создаёт пустой временный файл, принадлежащий вызывающей Music-сессии.</summary>
    string CreateTemporaryPath();
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
    private static readonly HttpClient SharedHttpClient = new();
    private readonly HttpClient httpClient;

    internal static DefaultMusicRuntime Instance { get; } = new();

    private DefaultMusicRuntime() : this(SharedHttpClient)
    {
    }

    // Клиент принадлежит вызывающему коду; runtime не освобождает его.
    internal DefaultMusicRuntime(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public IMusicOutput CreateOutput() => new NAudioMusicOutput();

    public IMusicFileSource OpenFile(string path) => new NAudioMusicFileSource(path);

    public async Task DownloadToFileAsync(
        string url,
        string path,
        long? maxBytes,
        CancellationToken cancellationToken)
    {
        if (maxBytes is long limit)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit, nameof(maxBytes));
        cancellationToken.ThrowIfCancellationRequested();

        using var response = await httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (maxBytes is long maximum && response.Content.Headers.ContentLength is long length && length > maximum)
            throw new InvalidDataException("Audio download exceeds the size limit.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        // CreateTemporaryPath уже зарезервировал файл для этой сессии.
        await using var output = new FileStream(
            path, FileMode.Truncate, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (maxBytes is long max && read > max - total)
                throw new InvalidDataException("Audio download exceeds the size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (maxBytes.HasValue)
                total += read;
        }
    }

    public string CreateTemporaryPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kid-audio-{Guid.NewGuid():N}.tmp");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        return path;
    }

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

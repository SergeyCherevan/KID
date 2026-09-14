using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace KID;

/// <summary>Управление файлами и уже созданными плеерами текущего запуска.</summary>
public static partial class Music
{
    /// <summary>Асинхронно запускает локальный файл или URL.</summary>
    public static SoundPlayer SoundPlay(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return new SoundPlayer(0);

        var scope = GetActiveScope();
        var playback = scope?.CreatePlayback(VolumeToAmplitude(Volume), filePath);
        if (scope == null || playback == null)
            return new SoundPlayer(0);

        try
        {
            scope.StartPlayback(playback, PlayFileCoreAsync);
            return playback.Player;
        }
        catch
        {
            scope.StopPlayerAsync(playback).GetAwaiter().GetResult();
            throw;
        }
    }

    /// <summary>Регистрирует файл в текущей сессии без запуска.</summary>
    public static SoundPlayer SoundLoad(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return new SoundPlayer(0);

        return GetActiveScope()?.CreatePlayback(VolumeToAmplitude(Volume), filePath)?.Player
               ?? new SoundPlayer(0);
    }

    /// <summary>Запускает ранее загруженный плеер или продолжает его после паузы.</summary>
    public static void SoundPlay(this SoundPlayer player)
    {
        var playback = GetOwnedPlayback(player);
        if (playback == null || playback.TryResume() || playback.GetCompletion() != null)
            return;

        playback.Scope.StartPlayback(
            playback,
            playback.SampleProviderFactory == null ? PlayFileCoreAsync : PlayGeneratedCoreAsync);
    }

    /// <summary>Ставит принадлежащий текущему запуску звук на паузу.</summary>
    public static void SoundPause(this SoundPlayer player) => GetOwnedPlayback(player)?.Pause();

    /// <summary>Останавливает звук и синхронно завершает его cleanup.</summary>
    public static void SoundStop(this SoundPlayer player)
    {
        var playback = GetOwnedPlayback(player);
        if (playback != null)
            playback.Scope.StopPlayerAsync(playback).GetAwaiter().GetResult();
    }

    /// <summary>Блокирует пользовательский поток до конца playback-задачи.</summary>
    public static void SoundWait(this SoundPlayer player)
    {
        var playback = player?.Playback;
        playback?.GetCompletion()?.GetAwaiter().GetResult();
    }

    /// <summary>Меняет громкость конкретного звука в диапазоне 0.0–1.0.</summary>
    public static void SoundVolume(this SoundPlayer player, double volume) =>
        GetOwnedPlayback(player)?.SetVolume(volume);

    /// <summary>Включает или выключает повторное воспроизведение.</summary>
    public static void SoundLoop(this SoundPlayer player, bool loop)
    {
        var playback = GetOwnedPlayback(player);
        if (playback != null)
            playback.Loop = loop;
    }

    /// <summary>Возвращает длительность звука.</summary>
    public static TimeSpan SoundLength(this SoundPlayer player) =>
        GetOwnedPlayback(player)?.Length ?? TimeSpan.Zero;

    /// <summary>Возвращает текущую позицию воспроизведения.</summary>
    public static TimeSpan SoundPosition(this SoundPlayer player) =>
        GetOwnedPlayback(player)?.Position ?? TimeSpan.Zero;

    /// <summary>Возвращает состояние воспроизведения.</summary>
    public static PlaybackState SoundState(this SoundPlayer player) =>
        GetOwnedPlayback(player)?.State ?? PlaybackState.Stopped;

    /// <summary>Перематывает файл к указанной позиции.</summary>
    public static void SoundSeek(this SoundPlayer player, TimeSpan position) =>
        GetOwnedPlayback(player)?.Seek(position);

    /// <summary>Запускает отменяемую, принадлежащую сессии задачу плавной громкости.</summary>
    public static void SoundFade(
        this SoundPlayer player,
        double fromVolume,
        double toVolume,
        TimeSpan duration)
    {
        var playback = GetOwnedPlayback(player);
        if (playback == null)
            return;

        playback.Scope.StartAuxiliary(playback, async token =>
        {
            if (duration <= TimeSpan.Zero)
            {
                playback.SetVolume(toVolume);
                return;
            }

            const int steps = 50;
            var delay = TimeSpan.FromMilliseconds(Math.Max(1, duration.TotalMilliseconds / steps));
            for (var step = 0; step <= steps; step++)
            {
                token.ThrowIfCancellationRequested();
                playback.SetVolume(fromVolume + ((toVolume - fromVolume) * step / steps));
                if (step < steps)
                    await Task.Delay(delay, token).ConfigureAwait(false);
            }
        });
    }

    /// <summary>Запускает плеер и асинхронно ожидает его завершения.</summary>
    public static async Task PlaySoundAsync(this SoundPlayer player)
    {
        player.SoundPlay();
        var completion = player?.Playback?.GetCompletion();
        if (completion != null)
            await completion.ConfigureAwait(false);
    }

    /// <summary>
    /// Пользовательская операция «остановить всё». Она очищает текущие звуки,
    /// но не закрывает Music-сессию: после неё пользователь может запустить новые.
    /// </summary>
    public static void SoundPlayerOFF() =>
        GetActiveScope()?.StopAllForUserAsync().GetAwaiter().GetResult();

    private static async Task PlayFileCoreAsync(
        MusicPlayback playback,
        CancellationToken cancellationToken)
    {
        var scope = playback.Scope;
        var filePath = playback.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        string? temporaryPath = null;
        try
        {
            var actualPath = filePath;
            if (IsUrl(filePath))
            {
                var content = await scope.Runtime.DownloadBytesAsync(filePath, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                temporaryPath = scope.Runtime.CreateTemporaryPath();
                scope.TrackTemporaryFile(temporaryPath);
                await scope.Runtime.WriteAllBytesAsync(temporaryPath, content, cancellationToken)
                    .ConfigureAwait(false);
                actualPath = temporaryPath;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!scope.Runtime.FileExists(actualPath))
                throw new FileNotFoundException($"Файл не найден: {filePath}", actualPath);

            var source = scope.Runtime.OpenFile(actualPath);
            var sourcePublished = false;
            try
            {
                source.Volume = (float)playback.Volume;
                playback.PublishAudioFile(source);
                sourcePublished = true;

                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await PlayProviderOnceAsync(playback, source.SampleProvider, cancellationToken)
                        .ConfigureAwait(false);
                    if (playback.Loop)
                        playback.Rewind();
                }
                while (playback.Loop);
            }
            finally
            {
                if (!sourcePublished)
                    source.Dispose();
            }
        }
        finally
        {
            if (temporaryPath != null)
                scope.DeleteTemporaryFile(temporaryPath);
        }
    }

    private static async Task PlayGeneratedCoreAsync(
        MusicPlayback playback,
        CancellationToken cancellationToken)
    {
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var factory = playback.SampleProviderFactory ??
                throw new InvalidOperationException("Generated sound has no sample provider factory.");
            var source = factory(cancellationToken);
            var volumeProvider = new VolumeSampleProvider(source)
            {
                Volume = (float)playback.Volume
            };
            playback.PublishVolumeProvider(volumeProvider);
            try
            {
                await PlayProviderOnceAsync(playback, volumeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                playback.ClearVolumeProvider(volumeProvider);
            }
        }
        while (playback.Loop);
    }

    private static async Task PlayProviderOnceAsync(
        MusicPlayback playback,
        ISampleProvider source,
        CancellationToken cancellationToken)
    {
        var output = playback.Scope.Runtime.CreateOutput();
        var published = false;
        try
        {
            playback.PublishOutput(output);
            published = true;
            output.Init(source);
            output.Play();

            while (output.PlaybackState is PlaybackState.Playing or PlaybackState.Paused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (published)
                playback.ReleaseOutput(output);
            else
                output.Dispose();
        }
    }

    private static bool IsUrl(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

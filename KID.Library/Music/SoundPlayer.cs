using NAudio.Wave;

namespace KID;

/// <summary>
/// Публичный handle одного звука. Ресурсами владеет не handle, а точная
/// execution-сессия Music, поэтому старый <see cref="Id"/> не может управлять
/// плеером следующего запуска даже при повторном использовании номера.
/// </summary>
public sealed class SoundPlayer : IDisposable
{
    private readonly MusicPlayback? playback;

    internal SoundPlayer(int id, MusicPlayback playback)
    {
        Id = id;
        this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
    }

    internal SoundPlayer(int id)
    {
        Id = id;
    }

    /// <summary>Идентификатор плеера внутри одной execution-сессии.</summary>
    public int Id { get; }

    /// <summary>Текущее состояние воспроизведения.</summary>
    public PlaybackState State => playback?.State ?? PlaybackState.Stopped;

    /// <summary>Текущая позиция воспроизведения.</summary>
    public TimeSpan Position => playback?.Position ?? TimeSpan.Zero;

    /// <summary>Общая длительность аудиофайла.</summary>
    public TimeSpan Length => playback?.Length ?? TimeSpan.Zero;

    internal MusicPlayback? Playback => playback;

    /// <summary>Останавливает этот звук и дожидается освобождения его ресурсов.</summary>
    public void Dispose() => Music.DisposePlayer(playback);
}


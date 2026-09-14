namespace KID;

/// <summary>Блокирующие Sound-перегрузки поверх единого управляемого playback.</summary>
public static partial class Music
{
    /// <summary>Воспроизводит тон и ждёт его окончания.</summary>
    public static void Sound(double frequency, double durationMs)
    {
        using var player = SoundPlay(frequency, durationMs);
        player.SoundWait();
    }

    /// <summary>Воспроизводит последовательность нот и ждёт её окончания.</summary>
    public static void Sound(params SoundNote[] notes) => Sound((IEnumerable<SoundNote>)notes);

    /// <summary>Воспроизводит последовательность нот и ждёт её окончания.</summary>
    public static void Sound(IEnumerable<SoundNote> notes)
    {
        if (notes == null)
            return;
        using var player = SoundPlay(notes);
        player.SoundWait();
    }

    /// <summary>Воспроизводит дорожки одновременно и ждёт их окончания.</summary>
    public static void Sound(params SoundNote[][] tracks) =>
        Sound((IEnumerable<IEnumerable<SoundNote>>)tracks);

    /// <summary>Воспроизводит дорожки одновременно и ждёт их окончания.</summary>
    public static void Sound(IEnumerable<IEnumerable<SoundNote>> tracks)
    {
        if (tracks == null)
            return;
        using var player = SoundPlay(tracks);
        player.SoundWait();
    }
}

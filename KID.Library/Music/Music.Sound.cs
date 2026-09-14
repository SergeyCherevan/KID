namespace KID;

/// <summary>Блокирующие Sound-перегрузки поверх единого управляемого playback.</summary>
public static partial class Music
{
    /// <summary>Воспроизводит тон и ждёт его окончания.</summary>
    public static void Sound(double frequency, double durationMs)
    {
        if (durationMs <= 0)
            return;

        var scope = GetActiveScope();
        if (scope == null)
            return;

        if (frequency == 0)
        {
            WaitForSilence(durationMs, scope.CancellationToken);
            return;
        }

        if (frequency > 0)
            PlayBlockingTone(scope, frequency, durationMs, VolumeToAmplitude(Volume));
    }

    /// <summary>Воспроизводит последовательность нот и ждёт её окончания.</summary>
    public static void Sound(params SoundNote[] notes) => Sound((IEnumerable<SoundNote>)notes);

    /// <summary>Воспроизводит последовательность нот и ждёт её окончания.</summary>
    public static void Sound(IEnumerable<SoundNote> notes)
    {
        if (notes == null)
            return;

        var scope = GetActiveScope();
        if (scope == null)
            return;

        var cancellationToken = scope.CancellationToken;
        using var enumerator = notes.GetEnumerator();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!enumerator.MoveNext())
                break;

            var note = enumerator.Current;
            if (note.DurationMs <= 0)
                continue;

            if (note.IsSilence)
            {
                WaitForSilence(note.DurationMs, cancellationToken);
                continue;
            }

            if (note.Frequency > 0)
                PlayBlockingTone(
                    scope,
                    note.Frequency,
                    note.DurationMs,
                    note.GetEffectiveVolume());
        }
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

    private static void PlayBlockingTone(
        MusicExecutionScope scope,
        double frequency,
        double durationMs,
        double amplitude)
    {
        using var player = StartGeneratedSound(
            token => CreateToneProvider(frequency, durationMs, amplitude, token),
            scope);
        player.SoundWait();
    }

    private static void WaitForSilence(double durationMs, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(durationMs), cancellationToken).GetAwaiter().GetResult();
}

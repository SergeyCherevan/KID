using NAudio.Wave;

namespace KID;

/// <summary>Асинхронный API генерации звуков внутри текущей execution-сессии.</summary>
public static partial class Music
{
    private const int GeneratedSampleRate = 44100;
    private const int GeneratedChannels = 1;

    /// <summary>Запускает тон и сразу возвращает управляемый плеер.</summary>
    public static SoundPlayer SoundPlay(double frequency, double durationMs)
    {
        if (durationMs <= 0)
            return new SoundPlayer(0);

        var amplitude = VolumeToAmplitude(Volume);
        return StartGeneratedSound(token => frequency == 0
            ? CreateSilenceProvider(durationMs, token)
            : CreateToneProvider(frequency, durationMs, amplitude, token));
    }

    /// <summary>Запускает последовательность нот и сразу возвращает плеер.</summary>
    public static SoundPlayer SoundPlay(params SoundNote[] notes) =>
        notes == null ? new SoundPlayer(0) : SoundPlay((IEnumerable<SoundNote>)notes);

    /// <summary>Запускает последовательность нот и сразу возвращает плеер.</summary>
    public static SoundPlayer SoundPlay(IEnumerable<SoundNote> notes)
    {
        if (notes == null)
            return new SoundPlayer(0);

        var scope = GetActiveScope();
        if (scope == null)
            return new SoundPlayer(0);

        var snapshot = SnapshotNotes(notes, scope.CancellationToken);
        if (snapshot.Length == 0 || snapshot.All(static note => note.DurationMs <= 0))
            return new SoundPlayer(0);

        return StartGeneratedSound(
            token => CreateMelodyProvider(snapshot, token),
            scope);
    }

    /// <summary>Запускает несколько дорожек одновременно и сразу возвращает плеер.</summary>
    public static SoundPlayer SoundPlay(params SoundNote[][] tracks) =>
        tracks == null
            ? new SoundPlayer(0)
            : SoundPlay((IEnumerable<IEnumerable<SoundNote>>)tracks);

    /// <summary>Запускает несколько дорожек одновременно и сразу возвращает плеер.</summary>
    public static SoundPlayer SoundPlay(IEnumerable<IEnumerable<SoundNote>> tracks)
    {
        if (tracks == null)
            return new SoundPlayer(0);

        var scope = GetActiveScope();
        if (scope == null)
            return new SoundPlayer(0);

        var token = scope.CancellationToken;
        var snapshots = new List<SoundNote[]>();
        using (var enumerator = tracks.GetEnumerator())
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (!enumerator.MoveNext())
                    break;
                if (enumerator.Current == null)
                    continue;

                var track = SnapshotNotes(enumerator.Current, token);
                if (track.Length > 0)
                    snapshots.Add(track);
            }
        }

        if (snapshots.Count == 0 ||
            snapshots.All(static track => track.All(static note => note.DurationMs <= 0)))
            return new SoundPlayer(0);

        var snapshot = snapshots.ToArray();
        return StartGeneratedSound(
            currentToken => CreatePolyphonyProvider(snapshot, currentToken),
            scope);
    }

    private static SoundPlayer StartGeneratedSound(
        Func<CancellationToken, ISampleProvider> sampleProviderFactory,
        MusicExecutionScope? knownScope = null)
    {
        ArgumentNullException.ThrowIfNull(sampleProviderFactory);
        var scope = knownScope ?? GetActiveScope();
        if (scope == null)
            return new SoundPlayer(0);

        var playback = scope.CreatePlayback(1.0, sampleProviderFactory: sampleProviderFactory);
        if (playback == null)
            return new SoundPlayer(0);

        try
        {
            scope.StartPlayback(playback, PlayGeneratedCoreAsync);
            return playback.Player;
        }
        catch
        {
            scope.StopPlayerAsync(playback).GetAwaiter().GetResult();
            throw;
        }
    }

    private static SoundNote[] SnapshotNotes(
        IEnumerable<SoundNote> notes,
        CancellationToken cancellationToken)
    {
        var snapshot = new List<SoundNote>();
        using var enumerator = notes.GetEnumerator();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!enumerator.MoveNext())
                break;
            snapshot.Add(enumerator.Current);
        }
        return snapshot.ToArray();
    }
}

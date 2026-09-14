using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID;

public static partial class Music
{
    private static ISampleProvider CreateToneProvider(
        double frequency,
        double durationMs,
        double volume,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (durationMs <= 0 || frequency <= 0 || volume <= 0)
            return CreateSilenceProvider(Math.Max(0, durationMs), cancellationToken);

        var generator = new SignalGenerator(GeneratedSampleRate, GeneratedChannels)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = Math.Clamp(frequency, 20, 20000),
            Gain = Math.Clamp(volume, 0.0, 1.0)
        };
        return new OffsetSampleProvider(generator)
        {
            Take = TimeSpan.FromMilliseconds(durationMs)
        };
    }

    private static ISampleProvider CreateSilenceProvider(
        double durationMs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generator = new SignalGenerator(GeneratedSampleRate, GeneratedChannels)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = 440,
            Gain = 0
        };
        return new OffsetSampleProvider(generator)
        {
            Take = TimeSpan.FromMilliseconds(Math.Max(0, durationMs))
        };
    }

    private static ISampleProvider CreateMelodyProvider(
        IReadOnlyList<SoundNote> notes,
        CancellationToken cancellationToken)
    {
        var parts = new List<ISampleProvider>(notes.Count);
        foreach (var note in notes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (note.DurationMs <= 0)
                continue;
            parts.Add(note.IsSilence
                ? CreateSilenceProvider(note.DurationMs, cancellationToken)
                : CreateToneProvider(
                    note.Frequency,
                    note.DurationMs,
                    note.GetEffectiveVolume(),
                    cancellationToken));
        }

        return parts.Count == 0
            ? CreateSilenceProvider(0, cancellationToken)
            : new ConcatenatingSampleProvider(parts);
    }
}

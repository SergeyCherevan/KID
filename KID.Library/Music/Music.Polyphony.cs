using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID;

public static partial class Music
{
    private static ISampleProvider CreatePolyphonyProvider(
        IReadOnlyList<SoundNote[]> tracks,
        CancellationToken cancellationToken)
    {
        var durations = new double[tracks.Count];
        var maximumDuration = 0.0;
        for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            foreach (var note in tracks[trackIndex])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (note.DurationMs > 0)
                    durations[trackIndex] += note.DurationMs;
            }
            maximumDuration = Math.Max(maximumDuration, durations[trackIndex]);
        }

        if (maximumDuration <= 0)
            return CreateSilenceProvider(0, cancellationToken);

        var mixer = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(GeneratedSampleRate, GeneratedChannels));
        for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ISampleProvider sequential = CreateMelodyProvider(
                tracks[trackIndex],
                cancellationToken);
            var padding = maximumDuration - durations[trackIndex];
            if (padding > 0)
            {
                sequential = new ConcatenatingSampleProvider(
                    [sequential, CreateSilenceProvider(padding, cancellationToken)]);
            }
            mixer.AddMixerInput(sequential);
        }
        return mixer;
    }
}

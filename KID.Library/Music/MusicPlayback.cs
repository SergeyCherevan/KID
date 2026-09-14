using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KID;

/// <summary>
/// Владеет ресурсами одного пользовательского <see cref="SoundPlayer"/> внутри точного
/// <see cref="MusicExecutionScope"/>. Числовой id служит только пользовательским handle;
/// lifecycle identity определяется ссылками на scope и этот объект.
/// </summary>
internal sealed class MusicPlayback
{
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetimeSource;
    private readonly HashSet<Task> auxiliaryTasks = [];
    private Task? completion;
    private IMusicOutput? output;
    private IMusicFileSource? audioFile;
    private VolumeSampleProvider? volumeProvider;
    private bool loop;
    private bool userStopRequested;
    private int resourcesDisposed;

    internal MusicPlayback(
        MusicExecutionScope scope,
        int id,
        double volume,
        string? filePath,
        Func<CancellationToken, ISampleProvider>? sampleProviderFactory)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        lifetimeSource = CancellationTokenSource.CreateLinkedTokenSource(scope.CancellationToken);
        Volume = volume;
        FilePath = filePath;
        SampleProviderFactory = sampleProviderFactory;
        Player = new SoundPlayer(id, this);
    }

    internal MusicExecutionScope Scope { get; }
    internal SoundPlayer Player { get; }
    internal CancellationToken CancellationToken => lifetimeSource.Token;
    internal string? FilePath { get; }
    internal Func<CancellationToken, ISampleProvider>? SampleProviderFactory { get; private set; }

    internal double Volume { get; private set; }

    internal bool Loop
    {
        get
        {
            lock (gate)
            {
                return loop;
            }
        }
        set
        {
            lock (gate)
            {
                loop = value;
            }
        }
    }

    internal bool UserStopRequested
    {
        get
        {
            lock (gate)
            {
                return userStopRequested;
            }
        }
    }

    internal PlaybackState State
    {
        get
        {
            lock (gate)
            {
                return output?.PlaybackState ?? PlaybackState.Stopped;
            }
        }
    }

    internal TimeSpan Position
    {
        get
        {
            lock (gate)
            {
                return audioFile?.CurrentTime ?? TimeSpan.Zero;
            }
        }
    }

    internal TimeSpan Length
    {
        get
        {
            lock (gate)
            {
                return audioFile?.TotalTime ?? TimeSpan.Zero;
            }
        }
    }

    internal void AttachCompletion(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (gate)
        {
            if (completion != null)
                throw new InvalidOperationException("Playback task is already attached.");
            completion = task;
        }
    }

    internal Task? GetCompletion()
    {
        lock (gate)
        {
            return completion;
        }
    }

    internal Task[] GetOwnedTasks()
    {
        lock (gate)
        {
            return completion == null
                ? auxiliaryTasks.ToArray()
                : [completion, .. auxiliaryTasks];
        }
    }

    internal void AddAuxiliaryTask(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (gate)
        {
            auxiliaryTasks.Add(task);
        }
    }

    internal void RemoveAuxiliaryTask(Task task)
    {
        lock (gate)
        {
            auxiliaryTasks.Remove(task);
        }
    }

    internal void PublishOutput(IMusicOutput value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(resourcesDisposed != 0, this);
            CancellationToken.ThrowIfCancellationRequested();
            if (output != null)
                throw new InvalidOperationException("Playback output is already active.");
            output = value;
        }
    }

    internal void ReleaseOutput(IMusicOutput value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            if (ReferenceEquals(output, value))
                output = null;
        }
        value.Dispose();
    }

    internal void PublishAudioFile(IMusicFileSource value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(resourcesDisposed != 0, this);
            CancellationToken.ThrowIfCancellationRequested();
            if (audioFile != null)
                throw new InvalidOperationException("Playback audio file is already active.");
            audioFile = value;
        }
    }

    internal void PublishVolumeProvider(VolumeSampleProvider value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(resourcesDisposed != 0, this);
            volumeProvider = value;
        }
    }

    internal void ClearVolumeProvider(VolumeSampleProvider value)
    {
        lock (gate)
        {
            if (ReferenceEquals(volumeProvider, value))
                volumeProvider = null;
        }
    }

    internal void SetVolume(double value)
    {
        value = Math.Max(0.0, Math.Min(1.0, value));
        lock (gate)
        {
            Volume = value;
            if (audioFile != null)
                audioFile.Volume = (float)value;
            if (volumeProvider != null)
                volumeProvider.Volume = (float)value;
        }
    }

    internal void Seek(TimeSpan position)
    {
        lock (gate)
        {
            if (audioFile != null)
                audioFile.CurrentTime = position;
        }
    }

    internal void Rewind()
    {
        lock (gate)
        {
            if (audioFile != null)
                audioFile.Position = 0;
        }
    }

    internal bool TryResume()
    {
        lock (gate)
        {
            if (output?.PlaybackState != PlaybackState.Paused)
                return false;
            output.Play();
            return true;
        }
    }

    internal void Pause()
    {
        lock (gate)
        {
            output?.Pause();
        }
    }

    internal void StopOutput()
    {
        IMusicOutput? owned;
        lock (gate)
        {
            owned = output;
        }
        owned?.Stop();
    }

    internal void RequestUserStop()
    {
        lock (gate)
        {
            userStopRequested = true;
            loop = false;
        }

        Exception? stopFailure = null;
        try
        {
            StopOutput();
        }
        catch (Exception exception)
        {
            stopFailure = exception;
        }
        finally
        {
            lifetimeSource.Cancel();
        }

        if (stopFailure != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stopFailure).Throw();
    }

    /// <summary>
    /// Вызывается после остановки и ожидания playback-задач. Каждая независимая операция
    /// освобождения получает попытку; ошибки возвращаются владельцу scope для агрегации.
    /// </summary>
    internal IReadOnlyList<Exception> DisposeResources()
    {
        if (Interlocked.Exchange(ref resourcesDisposed, 1) != 0)
            return [];

        List<Exception>? failures = null;
        IMusicOutput? ownedOutput;
        IMusicFileSource? ownedAudioFile;

        lock (gate)
        {
            loop = false;
            ownedOutput = output;
            output = null;
            ownedAudioFile = audioFile;
            audioFile = null;
            volumeProvider = null;
            SampleProviderFactory = null;
        }

        try { lifetimeSource.Cancel(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { ownedOutput?.Stop(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { ownedOutput?.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { ownedAudioFile?.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { lifetimeSource.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }

        return failures ?? [];
    }
}

namespace KID;

/// <summary>
/// Владеет всем Music runtime одной execution-сессии: активными плеерами,
/// playback/fade/download-задачами, linked cancellation и временными файлами.
/// </summary>
internal sealed class MusicExecutionScope : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetimeSource;
    private readonly Dictionary<int, MusicPlayback> activeSounds = [];
    private readonly HashSet<Task> backgroundTasks = [];
    private readonly HashSet<string> temporaryFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Exception> failures = [];
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int nextSoundId = 1;
    private bool closing;
    private bool shutdownStarted;

    internal MusicExecutionScope(ExecutionEnvironment environment, IMusicRuntime runtime)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        lifetimeSource = CancellationTokenSource.CreateLinkedTokenSource(environment.CancellationToken);
    }

    internal ExecutionEnvironment Environment { get; }
    internal IMusicRuntime Runtime { get; }
    internal CancellationToken CancellationToken => lifetimeSource.Token;

    internal bool IsAccepting
    {
        get
        {
            lock (gate)
            {
                return CanAcceptUnsafe();
            }
        }
    }

    internal int ActiveSoundCount
    {
        get
        {
            lock (gate)
            {
                return activeSounds.Count;
            }
        }
    }

    internal int BackgroundTaskCount
    {
        get
        {
            lock (gate)
            {
                return backgroundTasks.Count;
            }
        }
    }

    internal int TemporaryFileCount
    {
        get
        {
            lock (gate)
            {
                return temporaryFiles.Count;
            }
        }
    }

    internal MusicPlayback? CreatePlayback(
        double volume,
        string? filePath = null,
        Func<CancellationToken, NAudio.Wave.ISampleProvider>? sampleProviderFactory = null)
    {
        lock (gate)
        {
            Environment.ThrowIfCancellationRequested();
            if (!CanAcceptUnsafe())
                return null;

            var id = checked(nextSoundId++);
            var playback = new MusicPlayback(
                this,
                id,
                volume,
                filePath,
                sampleProviderFactory);
            activeSounds.Add(id, playback);
            return playback;
        }
    }

    internal bool Owns(MusicPlayback playback)
    {
        ArgumentNullException.ThrowIfNull(playback);
        lock (gate)
        {
            return ReferenceEquals(playback.Scope, this) &&
                   activeSounds.TryGetValue(playback.Player.Id, out var active) &&
                   ReferenceEquals(active, playback);
        }
    }

    internal Task StartPlayback(
        MusicPlayback playback,
        Func<MusicPlayback, CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentNullException.ThrowIfNull(operation);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task task;
        lock (gate)
        {
            if (!CanAcceptUnsafe() || !OwnsUnsafe(playback))
                throw new InvalidOperationException("Playback does not belong to the active Music scope.");
            if (playback.GetCompletion() != null)
                throw new InvalidOperationException("Playback is already started.");

            task = Task.Run(async () =>
            {
                await start.Task.ConfigureAwait(false);
                try
                {
                    await operation(playback, playback.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    playback.UserStopRequested &&
                    !Environment.CancellationToken.IsCancellationRequested)
                {
                }
                finally
                {
                    CompletePlayback(playback);
                }
            }, playback.CancellationToken);

            playback.AttachCompletion(task);
            backgroundTasks.Add(task);
        }

        ObserveTask(task, playback: null);
        start.TrySetResult();
        return task;
    }

    internal Task StartAuxiliary(
        MusicPlayback playback,
        Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentNullException.ThrowIfNull(operation);

        Task task;
        lock (gate)
        {
            if (!CanAcceptUnsafe() || !OwnsUnsafe(playback))
                return Task.CompletedTask;

            task = Task.Run(() => operation(playback.CancellationToken), playback.CancellationToken);
            playback.AddAuxiliaryTask(task);
            backgroundTasks.Add(task);
        }

        ObserveTask(task, playback);
        return task;
    }

    internal MusicPlayback[] SnapshotActiveSounds()
    {
        lock (gate)
        {
            return activeSounds.Values.ToArray();
        }
    }

    internal async Task StopPlayerAsync(MusicPlayback playback)
    {
        ArgumentNullException.ThrowIfNull(playback);
        if (!Owns(playback))
            return;

        Exception? stopFailure = null;
        try
        {
            playback.RequestUserStop();
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
            stopFailure = exception;
        }

        var tasks = playback.GetOwnedTasks();
        try
        {
            if (tasks.Length > 0)
                await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (playback.UserStopRequested) { }
        finally
        {
            CompletePlayback(playback);
        }

        if (stopFailure != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stopFailure).Throw();
    }

    internal async Task StopAllForUserAsync()
    {
        var snapshot = SnapshotActiveSounds();
        foreach (var playback in snapshot)
        {
            try { playback.RequestUserStop(); }
            catch (Exception exception) { RecordFailure(exception); }
        }

        var tasks = snapshot.SelectMany(static playback => playback.GetOwnedTasks()).Distinct().ToArray();
        try
        {
            if (tasks.Length > 0)
                await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { RecordFailure(exception); }

        foreach (var playback in snapshot)
            CompletePlayback(playback);
    }

    internal void TrackTemporaryFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (gate)
        {
            temporaryFiles.Add(path);
        }
    }

    internal void DeleteTemporaryFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            if (Runtime.FileExists(path))
                Runtime.DeleteFile(path);
            lock (gate)
            {
                temporaryFiles.Remove(path);
            }
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
    }

    internal void RecordFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lock (gate)
        {
            failures.Add(exception);
        }
    }

    public ValueTask DisposeAsync() => ShutdownAsync();

    internal ValueTask ShutdownAsync(Action? release = null)
    {
        bool startShutdown;
        lock (gate)
        {
            closing = true;
            startShutdown = !shutdownStarted;
            shutdownStarted = true;
        }

        if (startShutdown)
            _ = ShutdownCoreAsync(release);
        return new ValueTask(disposed.Task);
    }

    private bool CanAcceptUnsafe() =>
        !closing &&
        !lifetimeSource.IsCancellationRequested &&
        ExecutionEnvironmentManager.IsCurrentAndAccepting(Environment);

    private bool OwnsUnsafe(MusicPlayback playback) =>
        ReferenceEquals(playback.Scope, this) &&
        activeSounds.TryGetValue(playback.Player.Id, out var active) &&
        ReferenceEquals(active, playback);

    private void CompletePlayback(MusicPlayback playback)
    {
        lock (gate)
        {
            if (OwnsUnsafe(playback))
                activeSounds.Remove(playback.Player.Id);
        }

        foreach (var failure in playback.DisposeResources())
            RecordFailure(failure);
    }

    private void ObserveTask(Task task, MusicPlayback? playback)
    {
        _ = task.ContinueWith(
            static (completed, state) =>
            {
                var data = ((MusicExecutionScope Scope, MusicPlayback? Playback))state!;
                if (completed.Exception != null)
                {
                    foreach (var exception in completed.Exception.Flatten().InnerExceptions)
                    {
                        if (exception is not OperationCanceledException)
                            data.Scope.RecordFailure(exception);
                    }
                }

                data.Playback?.RemoveAuxiliaryTask(completed);
                lock (data.Scope.gate)
                {
                    data.Scope.backgroundTasks.Remove(completed);
                }
            },
            (this, playback),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ShutdownCoreAsync(Action? release)
    {
        var snapshot = SnapshotActiveSounds();

        foreach (var playback in snapshot)
        {
            try { playback.StopOutput(); }
            catch (Exception exception) { RecordFailure(exception); }
        }

        try { lifetimeSource.Cancel(); }
        catch (Exception exception) { RecordFailure(exception); }

        Task[] taskSnapshot;
        lock (gate)
        {
            taskSnapshot = backgroundTasks.ToArray();
        }

        if (taskSnapshot.Length > 0)
        {
            try { await Task.WhenAll(taskSnapshot).ConfigureAwait(false); }
            catch (OperationCanceledException) when (lifetimeSource.IsCancellationRequested) { }
            catch (Exception)
            {
                // Каждая unexpected task fault уже наблюдается ObserveTask и попадает в failures.
            }
        }

        foreach (var playback in snapshot)
            CompletePlayback(playback);

        string[] files;
        lock (gate)
        {
            files = temporaryFiles.ToArray();
        }
        foreach (var file in files)
            DeleteTemporaryFile(file);

        try { lifetimeSource.Dispose(); }
        catch (Exception exception) { RecordFailure(exception); }

        if (release != null)
        {
            try { release(); }
            catch (Exception exception) { RecordFailure(exception); }
        }

        Exception? failure;
        lock (gate)
        {
            activeSounds.Clear();
            backgroundTasks.Clear();
            temporaryFiles.Clear();
            failure = failures.Count switch
            {
                0 => null,
                1 => failures[0],
                _ => new AggregateException("Music cleanup failed.", failures.ToArray())
            };
        }

        if (failure == null)
            disposed.TrySetResult();
        else
            disposed.TrySetException(failure);
    }
}

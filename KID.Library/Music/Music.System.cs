namespace KID;

/// <summary>
/// Статический фасад Music над ресурсами текущего запуска. Единственным ambient
/// registry остаётся <see cref="ExecutionEnvironmentManager"/>; Music хранит лишь
/// capability, принадлежащую точной ссылке на environment.
/// </summary>
public static partial class Music
{
    private static readonly object _lockObject = new();
    private static MusicExecutionScope? executionScope;

    /// <summary>Инициализирует Music API для текущего запуска.</summary>
    public static void Init()
    {
        var environment = ExecutionEnvironmentManager.Current ??
            throw new InvalidOperationException("No execution is active.");
        Init(environment, DefaultMusicRuntime.Instance);
    }

    internal static MusicExecutionScope Init(
        ExecutionEnvironment environment,
        IMusicRuntime? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        environment.ThrowIfCancellationRequested();

        lock (_lockObject)
        {
            if (!ExecutionEnvironmentManager.IsCurrent(environment))
                throw new InvalidOperationException("Execution does not own the current environment.");
            if (executionScope != null)
                throw new InvalidOperationException("Music is already initialized for an execution.");

            var scope = new MusicExecutionScope(environment, runtime ?? DefaultMusicRuntime.Instance);
            Volatile.Write(ref executionScope, scope);
            return scope;
        }
    }

    internal static MusicExecutionScope? CurrentScope => Volatile.Read(ref executionScope);

    internal static ValueTask ShutdownAsync(ExecutionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var scope = CurrentScope;
        if (scope == null || !ReferenceEquals(scope.Environment, environment))
            return ValueTask.CompletedTask;
        return scope.ShutdownAsync(() => Release(scope));
    }

    internal static void DisposePlayer(MusicPlayback? playback)
    {
        if (playback == null)
            return;

        var scope = CurrentScope;
        if (scope != null &&
            ReferenceEquals(scope, playback.Scope) &&
            scope.Owns(playback))
        {
            scope.StopPlayerAsync(playback).GetAwaiter().GetResult();
            return;
        }

        foreach (var failure in playback.DisposeResources())
            playback.Scope.RecordFailure(failure);
    }

    private static MusicExecutionScope? GetActiveScope()
    {
        var scope = CurrentScope;
        return scope != null && scope.IsAccepting ? scope : null;
    }

    private static MusicPlayback? GetOwnedPlayback(SoundPlayer? player)
    {
        var playback = player?.Playback;
        var scope = CurrentScope;
        return playback != null &&
               scope != null &&
               ReferenceEquals(playback.Scope, scope) &&
               scope.Owns(playback)
            ? playback
            : null;
    }

    private static void Release(MusicExecutionScope scope)
    {
        lock (_lockObject)
        {
            if (ReferenceEquals(executionScope, scope))
                Volatile.Write(ref executionScope, null);
        }
    }
}


using System.Collections.Concurrent;
using System.Diagnostics;

namespace KID;

/// <summary>
/// Последовательно доставляет фоновые события в пределах одной execution-сессии.
/// Очередь, сигнал, linked token и все отложенные действия принадлежат только этому экземпляру.
/// </summary>
internal sealed class ExecutionEventWorker : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly ConcurrentQueue<Action> queue = new();
    private readonly SemaphoreSlim signal = new(0, int.MaxValue);
    private readonly CancellationTokenSource lifetimeSource;
    private readonly Task workerTask;
    private readonly HashSet<Task> delayedTasks = [];
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool closing;
    private bool shutdownStarted;

    internal ExecutionEventWorker(ExecutionEnvironment environment)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        lifetimeSource = CancellationTokenSource.CreateLinkedTokenSource(environment.CancellationToken);
        workerTask = Task.Run(WorkerLoopAsync);
    }

    internal ExecutionEnvironment Environment { get; }
    internal Task Completion => workerTask;
    internal int QueuedCount => queue.Count;

    internal bool IsAccepting
    {
        get
        {
            lock (gate)
            {
                return CanExecuteUnsafe();
            }
        }
    }

    /// <summary>Принимает действие, только пока исходная execution остаётся current и не закрывается.</summary>
    internal bool TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (gate)
        {
            if (!CanExecuteUnsafe()) return false;
            queue.Enqueue(action);
            signal.Release();
            return true;
        }
    }

    /// <summary>
    /// Регистрирует короткое отложенное действие в lifecycle worker. Shutdown отменяет delay и
    /// ожидает его завершение, поэтому pulse первого запуска не переживает cleanup.
    /// </summary>
    internal bool TrySchedule(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Task delayedTask;
        lock (gate)
        {
            if (!CanExecuteUnsafe()) return false;
            delayedTask = RunDelayedAsync(delay, action);
            delayedTasks.Add(delayedTask);
        }

        _ = delayedTask.ContinueWith(
            static (completed, state) => ((ExecutionEventWorker)state!).RemoveDelayed(completed),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return true;
    }

    /// <summary>Линейно закрывает приём до WPF-отписки, не дожидаясь асинхронной части cleanup.</summary>
    internal void Close()
    {
        lock (gate)
        {
            closing = true;
        }
    }

    public ValueTask DisposeAsync() => ShutdownAsync();

    /// <summary>
    /// Сначала закрывает приём и выполняет host-отписку, затем отменяет и ожидает worker и pulse,
    /// после чего выполняет compare-and-release cleanup владельца.
    /// </summary>
    internal ValueTask ShutdownAsync(Func<Task>? stopIngress = null, Action? cleanup = null)
    {
        bool startShutdown;
        lock (gate)
        {
            closing = true;
            startShutdown = !shutdownStarted;
            shutdownStarted = true;
        }

        if (startShutdown) _ = ShutdownCoreAsync(stopIngress, cleanup);
        return new ValueTask(disposed.Task);
    }

    private bool CanExecuteUnsafe() =>
        !closing &&
        !lifetimeSource.IsCancellationRequested &&
        ExecutionEnvironmentManager.IsCurrent(Environment);

    private bool CanExecute()
    {
        lock (gate)
        {
            return CanExecuteUnsafe();
        }
    }

    private async Task WorkerLoopAsync()
    {
        var token = lifetimeSource.Token;
        try
        {
            while (true)
            {
                await signal.WaitAsync(token).ConfigureAwait(false);
                while (queue.TryDequeue(out var action))
                {
                    if (!CanExecute()) return;
                    try
                    {
                        action();
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        // Ошибка одного пользовательского handler наблюдается, но не убивает
                        // worker и не превращает успешный resource cleanup в cleanup failure.
                        Trace.TraceError(exception.ToString());
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            while (queue.TryDequeue(out _)) { }
        }
    }

    private async Task RunDelayedAsync(TimeSpan delay, Action action)
    {
        var token = lifetimeSource.Token;
        try
        {
            await Task.Delay(delay, token).ConfigureAwait(false);
            if (!CanExecute()) return;
            action();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private void RemoveDelayed(Task completed)
    {
        lock (gate)
        {
            delayedTasks.Remove(completed);
        }
    }

    private async Task ShutdownCoreAsync(Func<Task>? stopIngress, Action? cleanup)
    {
        List<Exception>? failures = null;

        if (stopIngress != null)
        {
            try { await stopIngress().ConfigureAwait(false); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }

        try { lifetimeSource.Cancel(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }

        Task[] delayedSnapshot;
        lock (gate)
        {
            delayedSnapshot = delayedTasks.ToArray();
        }

        try
        {
            await Task.WhenAll([workerTask, .. delayedSnapshot]).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try { signal.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }
        try { lifetimeSource.Dispose(); }
        catch (Exception exception) { (failures ??= []).Add(exception); }

        if (cleanup != null)
        {
            try { cleanup(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }

        if (failures == null || failures.Count == 0) disposed.TrySetResult();
        else if (failures.Count == 1) disposed.TrySetException(failures[0]);
        else disposed.TrySetException(new AggregateException("Event worker cleanup failed.", failures));
    }
}

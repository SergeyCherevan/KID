using System.Collections.Concurrent;
using System.Windows.Threading;

namespace KID;

/// <summary>
/// Владеет принятыми Dispatcher-командами. Drain закрывает приём, но сохраняет принятый
/// вывод при штатном завершении. Stop отменяет pending-команды; выполняющиеся выходят кооперативно.
/// </summary>
internal sealed class DispatcherScope : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly HashSet<IWork> pending = [];
    private readonly ConcurrentQueue<Exception> failures = new();
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration registration;
    private bool closing;

    internal DispatcherScope(ExecutionEnvironment environment, Dispatcher dispatcher)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        registration = environment.CancellationToken.Register(AbortPending);
    }

    internal ExecutionEnvironment Environment { get; }
    internal long ExecutionId => Environment.ExecutionId;
    internal Dispatcher Dispatcher { get; }
    internal int PendingCount { get { lock (gate) return pending.Count; } }

    internal void CheckAccess(bool nested = false)
    {
        Environment.ThrowIfCancellationRequested();
        lock (gate)
            if (!ExecutionEnvironmentManager.IsCurrent(Environment) ||
                !Environment.OwnsDispatcher(this) ||
                (closing && !nested))
            {
                throw new ObjectDisposedException(nameof(DispatcherScope));
            }
    }

    internal Work<T> Post<T>(Func<T> action, bool reportFailure)
    {
        var work = new Work<T>(this, action, reportFailure);
        lock (gate)
        {
            CheckAccess();
            pending.Add(work);
            // Публикация и регистрация operation атомарны относительно Stop/Dispose.
            try { work.Operation = Dispatcher.InvokeAsync(work.Run, DispatcherPriority.Background); }
            catch { pending.Remove(work); throw; }
        }
        _ = work.ObserveAsync();
        if (Environment.CancellationToken.IsCancellationRequested) work.Abort();
        return work;
    }

    internal T RunInline<T>(Func<T> action)
    {
        var work = new Work<T>(this, action, reportFailure: false);
        lock (gate)
        {
            CheckAccess(DispatcherManager.IsExecuting(this));
            pending.Add(work);
        }
        try
        {
            work.Run();
            return work.Result.Task.GetAwaiter().GetResult();
        }
        finally { work.Finish(); }
    }

    private void AbortPending()
    {
        IWork[] snapshot;
        lock (gate) snapshot = pending.ToArray();
        foreach (var work in snapshot) work.Abort();
    }

    /// <summary>Не использует session token: host cleanup обязан работать после Stop.</summary>
    internal async Task OnUiAsync(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else await Dispatcher.InvokeAsync(action).Task.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ShutdownAsync(null);

    /// <summary>Ownership сохраняется до окончания переданного host-сброса Graphics.</summary>
    internal ValueTask ShutdownAsync(Action? cleanup)
    {
        Task[]? tasks = null;
        lock (gate)
        {
            if (!closing)
            {
                closing = true;
                tasks = pending.Select(work => work.Completion).ToArray();
            }
        }
        if (tasks != null) _ = DisposeCoreAsync(tasks, cleanup);
        return new ValueTask(disposed.Task);
    }

    private async Task DisposeCoreAsync(Task[] tasks, Action? cleanup)
    {
        try
        {
            if (Environment.CancellationToken.IsCancellationRequested) AbortPending();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            await registration.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) { failures.Enqueue(exception); }
        finally
        {
            try
            {
                if (cleanup != null) await OnUiAsync(cleanup).ConfigureAwait(false);
            }
            catch (Exception exception) { failures.Enqueue(exception); }
            DispatcherManager.Release(this);
        }

        var errors = failures.ToArray();
        failures.Clear();
        if (errors.Length == 0) disposed.TrySetResult();
        else disposed.TrySetException(errors.Length == 1 ? errors[0] : new AggregateException(errors));
    }

    private interface IWork
    {
        Task Completion { get; }
        void Abort();
    }

    internal sealed class Work<T> : IWork
    {
        private readonly DispatcherScope owner;
        private Func<T>? action;
        private readonly bool reportFailure;
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<T> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal DispatcherOperation? Operation { get; set; }
        public Task Completion => completion.Task;

        internal Work(DispatcherScope owner, Func<T> action, bool reportFailure)
        {
            this.owner = owner;
            this.action = action;
            this.reportFailure = reportFailure;
        }

        internal void Run()
        {
            try
            {
                if (!ExecutionEnvironmentManager.IsCurrent(owner.Environment) ||
                    !owner.Environment.OwnsDispatcher(owner))
                {
                    throw new ObjectDisposedException(nameof(DispatcherScope));
                }
                owner.Environment.ThrowIfCancellationRequested();
                Result.TrySetResult(DispatcherManager.Execute(owner, action!));
            }
            catch (OperationCanceledException exception) when (
                owner.Environment.CancellationToken.IsCancellationRequested &&
                exception.CancellationToken == owner.Environment.CancellationToken)
            {
                Result.TrySetCanceled(owner.Environment.CancellationToken);
            }
            catch (Exception exception)
            {
                // После отмены waiter уже мог уйти. Не теряем поздний неожиданный fault.
                if (reportFailure || owner.Environment.CancellationToken.IsCancellationRequested)
                    owner.failures.Enqueue(exception);
                Result.TrySetException(exception);
            }
            finally { action = null; }
        }

        public void Abort() => Operation?.Abort();

        internal async Task ObserveAsync()
        {
            try { await Operation!.Task.ConfigureAwait(false); }
            catch (OperationCanceledException) when (owner.Environment.CancellationToken.IsCancellationRequested)
            {
                Result.TrySetCanceled(owner.Environment.CancellationToken);
            }
            catch (Exception exception)
            {
                // Dispatcher shutdown без Stop — ошибка host, а не успешная отмена.
                owner.failures.Enqueue(exception);
                Result.TrySetException(exception);
            }
            finally { Finish(); }
        }

        internal void Finish()
        {
            action = null;
            _ = Result.Task.Exception;
            lock (owner.gate)
            {
                completion.TrySetResult();
                owner.pending.Remove(this);
            }
        }
    }
}

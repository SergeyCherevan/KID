using System.Windows.Threading;

namespace KID;

/// <summary>
/// Представляет единую ambient identity одного запуска внутри KID.Library.
/// Не управляет полным lifecycle: этим владеет host-сессия. Environment публикует
/// неизменяемые id/token и подключаемые runtime-capabilities этого запуска.
/// </summary>
internal sealed class ExecutionEnvironment
{
    private readonly object capabilityGate = new();
    private DispatcherScope? dispatcherScope;
    private int cleanupStarted;

    internal ExecutionEnvironment(long executionId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionId);
        ExecutionId = executionId;
        CancellationToken = cancellationToken;
    }

    internal long ExecutionId { get; }
    internal CancellationToken CancellationToken { get; }
    internal bool IsAcceptingNewWork => Volatile.Read(ref cleanupStarted) == 0;

    internal void BeginCleanup() => Interlocked.Exchange(ref cleanupStarted, 1);

    /// <summary>Единственная реализация проверки Stop для состояния execution.</summary>
    internal void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    internal DispatcherScope AttachDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        lock (capabilityGate)
        {
            if (!IsAcceptingNewWork)
                throw new ObjectDisposedException(nameof(ExecutionEnvironment));
            if (dispatcherScope != null)
                throw new InvalidOperationException("A dispatcher is already attached to this execution.");

            var scope = new DispatcherScope(this, dispatcher);
            Volatile.Write(ref dispatcherScope, scope);
            return scope;
        }
    }

    internal DispatcherScope GetDispatcher() =>
        Volatile.Read(ref dispatcherScope) ??
        throw new InvalidOperationException("No graphics execution is active.");

    /// <summary>
    /// Lock-free read не создаёт порядок scope gate → environment gate и тем самым
    /// исключает инверсию блокировок с синхронным cancellation callback конструктора scope.
    /// </summary>
    internal bool OwnsDispatcher(DispatcherScope scope) =>
        ReferenceEquals(Volatile.Read(ref dispatcherScope), scope);

    internal void ReleaseDispatcher(DispatcherScope scope)
    {
        lock (capabilityGate)
        {
            if (ReferenceEquals(dispatcherScope, scope))
                Volatile.Write(ref dispatcherScope, null);
        }
    }
}

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
    private ExecutionDispatcherScope? dispatcherScope;

    internal ExecutionEnvironment(long executionId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionId);
        ExecutionId = executionId;
        CancellationToken = cancellationToken;
    }

    internal long ExecutionId { get; }
    internal CancellationToken CancellationToken { get; }

    /// <summary>Единственная реализация проверки Stop для состояния execution.</summary>
    internal void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    internal ExecutionDispatcherScope AttachDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        lock (capabilityGate)
        {
            if (dispatcherScope != null)
                throw new InvalidOperationException("A dispatcher is already attached to this execution.");

            var scope = new ExecutionDispatcherScope(this, dispatcher);
            Volatile.Write(ref dispatcherScope, scope);
            return scope;
        }
    }

    internal ExecutionDispatcherScope GetDispatcher() =>
        Volatile.Read(ref dispatcherScope) ??
        throw new InvalidOperationException("No graphics execution is active.");

    /// <summary>
    /// Lock-free read не создаёт порядок scope gate → environment gate и тем самым
    /// исключает инверсию блокировок с синхронным cancellation callback конструктора scope.
    /// </summary>
    internal bool OwnsDispatcher(ExecutionDispatcherScope scope) =>
        ReferenceEquals(Volatile.Read(ref dispatcherScope), scope);

    internal void ReleaseDispatcher(ExecutionDispatcherScope scope)
    {
        lock (capabilityGate)
        {
            if (ReferenceEquals(dispatcherScope, scope))
                Volatile.Write(ref dispatcherScope, null);
        }
    }
}

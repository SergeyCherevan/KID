using System.Windows.Threading;
using System.Diagnostics;

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

    internal ExecutionEnvironment(
        long executionId,
        CancellationToken cancellationToken,
        Action<ExecutionDiagnostic>? diagnosticSink = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionId);
        ExecutionId = executionId;
        CancellationToken = cancellationToken;
        this.diagnosticSink = diagnosticSink;
    }

    private readonly Action<ExecutionDiagnostic>? diagnosticSink;

    internal long ExecutionId { get; }
    internal CancellationToken CancellationToken { get; }
    internal bool IsAcceptingNewWork => Volatile.Read(ref cleanupStarted) == 0;

    internal void BeginCleanup() => Interlocked.Exchange(ref cleanupStarted, 1);

    /// <summary>Единственная реализация проверки Stop для состояния execution.</summary>
    internal void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    internal void ReportDiagnostic(string component, string operation, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            diagnosticSink?.Invoke(new ExecutionDiagnostic(ExecutionId, component, operation, exception));
        }
        catch (Exception sinkException)
        {
            Trace.TraceError(
                "Execution diagnostic sink failed. ExecutionId={0}; Component={1}; Operation={2}; Exception={3}",
                ExecutionId,
                component,
                operation,
                sinkException);
        }
    }

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

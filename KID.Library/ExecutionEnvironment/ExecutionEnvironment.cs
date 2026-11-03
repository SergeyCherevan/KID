using System.Threading;
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
    private readonly Lock capabilityGate = new();
    private DispatcherScope? dispatcherScope;
    private Dispatcher? uiDispatcher;
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

    private Action<ExecutionDiagnostic>? diagnosticSink;

    internal void ClearDiagnosticSink() => Volatile.Write(ref diagnosticSink, null);

    internal long ExecutionId { get; }
    internal CancellationToken CancellationToken { get; }
    internal bool IsAcceptingNewWork => Volatile.Read(ref cleanupStarted) == 0;

    internal void BeginCleanup()
    {
        lock (capabilityGate) Interlocked.Exchange(ref cleanupStarted, 1);
    }

    // Short synchronous mutations share the admission boundary with BeginCleanup.
    // Unlike component attachment this can be entered from a worker without touching WPF state.
    internal Lock.Scope EnterWork()
    {
        var admission = capabilityGate.EnterScope();
        if (!ExecutionEnvironmentManager.IsCurrentAndAccepting(this) || CancellationToken.IsCancellationRequested)
        {
            admission.Dispose();
            throw new ObjectDisposedException(nameof(ExecutionEnvironment));
        }
        return admission;
    }

    // Serialize component publication with ingress closure, including legacy public Init calls.
    internal Lock.Scope EnterAttachment(Dispatcher? dispatcher = null)
    {
        var attachment = capabilityGate.EnterScope();
        try
        {
            ThrowIfCancellationRequested();
            if (!ExecutionEnvironmentManager.IsCurrentAndAccepting(this))
                throw new InvalidOperationException("Execution does not own the current environment.");
            if (dispatcher != null)
            {
                dispatcher.VerifyAccess();
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    throw new InvalidOperationException("The UI Dispatcher is shutting down.");
                BindDispatcher(dispatcher);
            }
            return attachment;
        }
        catch
        {
            attachment.Dispose();
            throw;
        }
    }

    internal DispatcherScope? AttachedDispatcher => Volatile.Read(ref dispatcherScope);

    /// <summary>Единственная реализация проверки Stop для состояния execution.</summary>
    internal void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();

    internal void ReportDiagnostic(string component, string operation, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            Volatile.Read(ref diagnosticSink)?.Invoke(new ExecutionDiagnostic(ExecutionId, component, operation, exception));
        }
        catch (Exception sinkException)
        {
            try
            {
                Trace.TraceError(
                    "Execution diagnostic sink failed. ExecutionId={0}; Component={1}; Operation={2}; Exception={3}",
                    ExecutionId,
                    component,
                    operation,
                    sinkException);
            }
            catch (Exception)
            {
                // A host TraceListener must not make diagnostics interrupt mandatory cleanup.
            }
        }
    }

    internal DispatcherScope AttachDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        using var attachment = EnterAttachment();
        if (dispatcherScope != null)
            throw new InvalidOperationException("A dispatcher is already attached to this execution.");
        // The low-level scope also supports testing Dispatcher shutdown from another thread.
        // UI control access/liveness is validated by the session and component entry points.
        BindDispatcher(dispatcher);
        var scope = new DispatcherScope(this, dispatcher);
        Volatile.Write(ref dispatcherScope, scope);
        return scope;
    }

    private void BindDispatcher(Dispatcher dispatcher)
    {
        if (uiDispatcher != null && !ReferenceEquals(uiDispatcher, dispatcher))
            throw new ArgumentException("All runtime controls must use the same Dispatcher.", nameof(dispatcher));
        uiDispatcher = dispatcher;
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

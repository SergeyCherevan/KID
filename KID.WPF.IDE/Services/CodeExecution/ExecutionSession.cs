using KID.Services.Errors;

namespace KID.Services.CodeExecution;

/// <summary>IDE state and completion for one runtime-owned identity and cancellation token.</summary>
internal sealed class ExecutionSession
{
    private readonly ExecutionFailureCollector stateNotificationFailures = new();
    private int stopRequestCount;

    internal ExecutionSession(RuntimeSessionOwner runtime) =>
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    internal RuntimeSessionOwner Runtime { get; }
    public long ExecutionId => Runtime.Environment.ExecutionId;
    public CancellationToken CancellationToken => Runtime.CancellationToken;
    public ExecutionState State { get; private set; } = ExecutionState.Idle;
    public Task? ActiveTask { get; private set; }

    internal void RecordStateNotificationFailure(Exception exception) => stateNotificationFailures.Add(exception);
    internal void DrainStateNotificationFailuresTo(ExecutionFailureCollector destination) =>
        stateNotificationFailures.DrainTo(destination);

    public void AttachTask(Task activeTask)
    {
        ArgumentNullException.ThrowIfNull(activeTask);
        if (ActiveTask != null) throw new InvalidOperationException("The execution task is already attached.");
        ActiveTask = activeTask;
    }

    public bool RequestStop()
    {
        if (Interlocked.Exchange(ref stopRequestCount, 1) != 0) return false;
        Runtime.RequestStop();
        return true;
    }

    public ExecutionStateChangedEventArgs TransitionTo(ExecutionState newState)
    {
        if (!IsValidTransition(State, newState))
            throw new InvalidOperationException($"Invalid execution state transition: {State} -> {newState}.");
        var previous = State;
        State = newState;
        return new ExecutionStateChangedEventArgs(ExecutionId, previous, newState);
    }

    private static bool IsValidTransition(ExecutionState current, ExecutionState next) => (current, next) switch
    {
        (ExecutionState.Idle, ExecutionState.Compiling) => true,
        (ExecutionState.Compiling, ExecutionState.Running or ExecutionState.StopRequested or ExecutionState.CleaningUp) => true,
        (ExecutionState.Running, ExecutionState.StopRequested or ExecutionState.CleaningUp) => true,
        (ExecutionState.StopRequested, ExecutionState.CleaningUp) => true,
        (ExecutionState.CleaningUp, ExecutionState.Idle) => true,
        _ => false
    };
}

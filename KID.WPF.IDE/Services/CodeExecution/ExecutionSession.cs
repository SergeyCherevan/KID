using System;
using System.Threading;
using System.Threading.Tasks;

namespace KID.Services.CodeExecution
{
    internal sealed class ExecutionSession : IDisposable
    {
        private readonly CancellationTokenSource cancellationSource = new();
        private int stopRequestCount;
        private int isDisposed;

        public ExecutionSession(long executionId)
        {
            if (executionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(executionId));

            ExecutionId = executionId;
        }

        public long ExecutionId { get; }

        public CancellationToken CancellationToken => cancellationSource.Token;

        public ExecutionState State { get; private set; } = ExecutionState.Idle;

        public Task? ActiveTask { get; private set; }

        public void AttachTask(Task activeTask)
        {
            if (activeTask == null)
                throw new ArgumentNullException(nameof(activeTask));
            if (ActiveTask != null)
                throw new InvalidOperationException("The execution task is already attached.");

            ActiveTask = activeTask;
        }

        public bool RequestStop()
        {
            if (Interlocked.Exchange(ref stopRequestCount, 1) != 0)
                return false;

            cancellationSource.Cancel();
            return true;
        }

        public ExecutionStateChangedEventArgs TransitionTo(ExecutionState newState)
        {
            if (!IsValidTransition(State, newState))
            {
                throw new InvalidOperationException(
                    $"Invalid execution state transition: {State} -> {newState}.");
            }

            var previousState = State;
            State = newState;
            return new ExecutionStateChangedEventArgs(
                ExecutionId,
                previousState,
                newState);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref isDisposed, 1) == 0)
                cancellationSource.Dispose();
        }

        private static bool IsValidTransition(
            ExecutionState currentState,
            ExecutionState newState) =>
            (currentState, newState) switch
            {
                (ExecutionState.Idle, ExecutionState.Compiling) => true,
                (ExecutionState.Compiling, ExecutionState.Running) => true,
                (ExecutionState.Compiling, ExecutionState.StopRequested) => true,
                (ExecutionState.Compiling, ExecutionState.CleaningUp) => true,
                (ExecutionState.Running, ExecutionState.StopRequested) => true,
                (ExecutionState.Running, ExecutionState.CleaningUp) => true,
                (ExecutionState.StopRequested, ExecutionState.CleaningUp) => true,
                (ExecutionState.CleaningUp, ExecutionState.Idle) => true,
                _ => false
            };
    }
}

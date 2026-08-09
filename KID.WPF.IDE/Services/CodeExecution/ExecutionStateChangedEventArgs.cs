using System;

namespace KID.Services.CodeExecution
{
    public sealed class ExecutionStateChangedEventArgs : EventArgs
    {
        public ExecutionStateChangedEventArgs(
            long executionId,
            ExecutionState previousState,
            ExecutionState currentState)
        {
            ExecutionId = executionId;
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public long ExecutionId { get; }

        public ExecutionState PreviousState { get; }

        public ExecutionState CurrentState { get; }
    }
}

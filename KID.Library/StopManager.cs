using System.Threading;

namespace KID
{
    public static class StopManager
    {
        private static readonly object _lockObject = new object();
        private static CancellationToken _currentToken;
        private static long? _currentExecutionId;

        public static CancellationToken CurrentToken
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentToken;
                }
            }
        }

        internal static IDisposable BeginExecution(long executionId, CancellationToken cancellationToken)
        {
            if (executionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(executionId));

            lock (_lockObject)
            {
                if (_currentExecutionId.HasValue)
                    throw new InvalidOperationException("An execution token is already active.");

                _currentExecutionId = executionId;
                _currentToken = cancellationToken;
            }

            return new ExecutionTokenLease(executionId);
        }

        public static void StopIfButtonPressed()
        {
            CancellationToken token;
            lock (_lockObject)
            {
                token = _currentToken;
            }

            if (token != default)
            {
                token.ThrowIfCancellationRequested();
            }
        }

        private static void EndExecution(long executionId)
        {
            lock (_lockObject)
            {
                if (_currentExecutionId != executionId)
                    return;

                _currentExecutionId = null;
                _currentToken = default;
            }
        }

        private sealed class ExecutionTokenLease : IDisposable
        {
            private readonly long executionId;
            private int isDisposed;

            public ExecutionTokenLease(long executionId)
            {
                this.executionId = executionId;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref isDisposed, 1) == 0)
                    EndExecution(executionId);
            }
        }
    }
}

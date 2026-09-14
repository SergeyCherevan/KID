namespace KID;

/// <summary>
/// Единственный registry ambient execution в KID.Library. StopManager и
/// DispatcherManager являются фасадами над опубликованным environment.
/// </summary>
internal static class ExecutionEnvironmentManager
{
    private static readonly object gate = new();
    private static ExecutionEnvironment? current;

    internal static ExecutionEnvironment? Current => Volatile.Read(ref current);

    internal static IDisposable BeginExecution(long executionId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionId);
        lock (gate)
        {
            if (current != null)
                throw new InvalidOperationException("An execution is already active.");

            var environment = new ExecutionEnvironment(executionId, cancellationToken);
            Volatile.Write(ref current, environment);
            return new ExecutionEnvironmentLease(environment);
        }
    }

    internal static ExecutionEnvironment GetCurrent(long executionId)
    {
        var environment = Current ??
            throw new InvalidOperationException("No execution is active.");
        if (environment.ExecutionId != executionId)
            throw new InvalidOperationException("Execution does not own the current environment.");
        return environment;
    }

    /// <summary>
    /// Проверка identity и публикация capability атомарны относительно release environment.
    /// Scope нельзя прикрепить к ссылке, которая между этими действиями перестала быть current.
    /// </summary>
    internal static ExecutionDispatcherScope AttachDispatcher(
        long executionId,
        System.Windows.Threading.Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        lock (gate)
        {
            var environment = current ??
                throw new InvalidOperationException("No execution is active.");
            if (environment.ExecutionId != executionId)
                throw new InvalidOperationException("Execution does not own the current environment.");
            return environment.AttachDispatcher(dispatcher);
        }
    }

    internal static bool IsCurrent(ExecutionEnvironment environment) =>
        ReferenceEquals(Current, environment);

    private static void Release(ExecutionEnvironment environment)
    {
        lock (gate)
        {
            if (ReferenceEquals(current, environment))
                Volatile.Write(ref current, null);
        }
    }

    private sealed class ExecutionEnvironmentLease(ExecutionEnvironment environment) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                Release(environment);
        }
    }
}

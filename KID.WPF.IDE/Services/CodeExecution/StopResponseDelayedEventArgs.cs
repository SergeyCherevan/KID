namespace KID.Services.CodeExecution;

/// <summary>
/// Сообщает, что сессия остаётся в StopRequested дольше диагностического интервала.
/// </summary>
public sealed class StopResponseDelayedEventArgs(long executionId) : EventArgs
{
    public long ExecutionId { get; } = executionId;
}

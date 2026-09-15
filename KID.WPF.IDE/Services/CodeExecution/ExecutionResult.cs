namespace KID.Services.CodeExecution;

/// <summary>
/// Неизменяемый терминальный результат одного запуска пользовательской программы.
/// </summary>
/// <remarks>
/// Результат не является состоянием активной lifecycle-машины. Он публикуется только после
/// завершения entry point и подтверждённого cleanup, когда новый Run уже может быть разрешён.
/// </remarks>
public sealed record ExecutionResult(
    ExecutionResultKind Kind,
    string? ErrorMessage = null,
    string? StackTrace = null)
{
    public static ExecutionResult Rejected { get; } = new(ExecutionResultKind.Rejected);
    public static ExecutionResult NoEntryPoint { get; } = new(ExecutionResultKind.NoEntryPoint);
    public static ExecutionResult CompilationFailed { get; } = new(ExecutionResultKind.CompilationFailed);
    public static ExecutionResult Completed { get; } = new(ExecutionResultKind.Completed);
    public static ExecutionResult Stopped { get; } = new(ExecutionResultKind.Stopped);

    public static ExecutionResult RuntimeFaulted(string message, string? stackTrace) =>
        new(ExecutionResultKind.RuntimeFaulted, message, stackTrace);
}

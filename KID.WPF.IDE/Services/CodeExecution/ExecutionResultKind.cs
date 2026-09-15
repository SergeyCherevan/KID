namespace KID.Services.CodeExecution;

/// <summary>Возможные терминальные результаты завершённого execution lifecycle.</summary>
public enum ExecutionResultKind
{
    Rejected = 0,
    NoEntryPoint = 1,
    CompilationFailed = 2,
    Completed = 3,
    Stopped = 4,
    RuntimeFaulted = 5
}

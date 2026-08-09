namespace KID.Services.CodeExecution
{
    public enum ExecutionState
    {
        Idle,
        Compiling,
        Running,
        StopRequested,
        CleaningUp
    }
}

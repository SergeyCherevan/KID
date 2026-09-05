namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface IConsoleContext : IAsyncDisposable
    {
        object ConsoleTarget { get; set; }

        void Init(long executionId, CancellationToken cancellationToken);
    }
}

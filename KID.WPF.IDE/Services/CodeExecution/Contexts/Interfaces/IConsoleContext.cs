namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface IConsoleContext : IAsyncDisposable
    {
        object ConsoleTarget { get; set; }

        void Init(long executionId, CancellationToken cancellationToken);

        /// <summary>
        /// Идемпотентно закрывает приём нового ввода/вывода, не восстанавливая streams раньше
        /// завершения остальных execution workers.
        /// </summary>
        void BeginCleanup();
    }
}

namespace KID.Services.CodeExecution.Interfaces
{
    /// <summary>
    /// Загружает и выполняет скомпилированный PE/PDB-артефакт пользовательской программы.
    /// </summary>
    public interface ICodeRunner
    {
        /// <summary>
        /// Асинхронно загружает артефакт и выполняет его entry point.
        /// </summary>
        /// <param name="artifact">Успешный результат стадии компиляции.</param>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        Task RunAsync(
            CompilationArtifact artifact,
            CancellationToken cancellationToken = default);
    }
}

namespace KID.Services.CodeExecution.Interfaces
{
    /// <summary>
    /// Создаёт и запускает отдельное выполнение скомпилированного PE/PDB-артефакта.
    /// </summary>
    public interface ICodeRunner
    {
        /// <summary>
        /// Запускает программу и возвращает владеющий её ресурсами экземпляр без ожидания завершения.
        /// </summary>
        /// <param name="artifact">Успешный результат стадии компиляции.</param>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        /// <returns>
        /// Уже запущенный экземпляр. Coordinator ожидает Completion и освобождает экземпляр
        /// после очистки execution-контекста, в том числе при ошибке или отмене выполнения.
        /// </returns>
        /// <remarks>
        /// Ошибки выполнения передаются через Completion, чтобы экземпляр оставался доступен
        /// для cleanup. Если Start не может вернуть экземпляр, созданные ресурсы остаются
        /// ответственностью runner. Проверки аргументов выполняются до создания ресурсов запуска.
        /// </remarks>
        ICodeRunningInstance Start(
            CompilationArtifact artifact,
            CancellationToken cancellationToken = default);
    }
}

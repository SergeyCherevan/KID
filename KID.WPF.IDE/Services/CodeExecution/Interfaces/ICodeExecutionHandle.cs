namespace KID.Services.CodeExecution.Interfaces
{
    /// <summary>
    /// Владеет runtime-ресурсами одного запуска пользовательской программы.
    /// </summary>
    /// <remarks>
    /// Handle является execution-scoped объектом: один экземпляр допускает только один
    /// <see cref="RunAsync(CancellationToken)"/>. После завершения запуска coordinator вызывает
    /// <see cref="IDisposable.Dispose"/>, чтобы инициировать выгрузку принадлежащего программе
    /// collectible AssemblyLoadContext.
    /// </remarks>
    public interface ICodeExecutionHandle : IDisposable
    {
        /// <summary>
        /// Загружает артефакт и выполняет его entry point.
        /// </summary>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        Task RunAsync(CancellationToken cancellationToken = default);
    }
}

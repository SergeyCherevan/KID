namespace KID.Services.CodeExecution.Interfaces
{
    /// <summary>
    /// Создаёт отдельное выполнение скомпилированного PE/PDB-артефакта.
    /// </summary>
    public interface ICodeRunner
    {
        /// <summary>
        /// Создаёт execution-scoped handle, который владеет runtime-загрузкой одной программы.
        /// </summary>
        /// <param name="artifact">Успешный результат стадии компиляции.</param>
        /// <returns>
        /// Новый handle. Coordinator обязан освободить его после завершения выполнения и
        /// очистки execution-контекста.
        /// </returns>
        ICodeExecutionHandle CreateExecution(CompilationArtifact artifact);
    }
}

namespace KID.Services.CodeExecution.Interfaces
{
    /// <summary>
    /// Компилирует пользовательский исходный код в PE/PDB-артефакт без загрузки сборки в CLR.
    /// </summary>
    public interface ICodeCompiler
    {
        /// <summary>
        /// Асинхронно выполняет компиляцию либо возвращает штатные пользовательские ошибки.
        /// </summary>
        /// <param name="code">Полный текст пользовательской программы.</param>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        /// <returns>Результат с артефактом или локализованными ошибками.</returns>
        Task<CompilationResult> CompileAsync(
            string code,
            CancellationToken cancellationToken = default);
    }
}

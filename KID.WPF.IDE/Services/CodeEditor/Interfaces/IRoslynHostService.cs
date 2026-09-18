using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor.Interfaces
{
    /// <summary>
    /// Сервис для предоставления сконфигурированного экземпляра RoslynHost (IntelliSense, анализ кода).
    /// </summary>
    public interface IRoslynHostService
    {
        /// <summary>
        /// Возвращает единственный RoslynHost с детерминированными references и явными imports
        /// общего compilation profile.
        /// </summary>
        RoslynHost GetHost();
    }
}

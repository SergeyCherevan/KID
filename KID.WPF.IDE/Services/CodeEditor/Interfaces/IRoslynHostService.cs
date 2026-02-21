using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor.Interfaces
{
    /// <summary>
    /// Сервис для предоставления сконфигурированного экземпляра RoslynHost (IntelliSense, анализ кода).
    /// </summary>
    public interface IRoslynHostService
    {
        /// <summary>
        /// Возвращает единственный экземпляр RoslynHost с подключёнными ссылками на KID.Library, NAudio и стандартными импортами.
        /// </summary>
        RoslynHost GetHost();
    }
}

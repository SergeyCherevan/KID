using ICSharpCode.AvalonEdit;

namespace KID.Services.CodeEditor.Interfaces
{
    /// <summary>
    /// Фабрика для создания экземпляров редактора кода с настройками из IWindowConfigurationService.
    /// </summary>
    public interface ICodeEditorFactory
    {
        /// <summary>
        /// Создаёт экземпляр TextEditor с заданным содержимым и подсветкой синтаксиса.
        /// Шрифт и цвета берутся из настроек окна.
        /// </summary>
        /// <param name="content">Начальное содержимое редактора.</param>
        /// <param name="programmingLanguage">Язык программирования для подсветки синтаксиса (например, "C#").</param>
        /// <returns>Сконфигурированный экземпляр TextEditor.</returns>
        TextEditor Create(string content, string programmingLanguage);
    }
}

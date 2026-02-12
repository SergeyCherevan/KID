using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.Initialize.Interfaces;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Реализация фабрики редакторов кода. Создаёт TextEditor с настройками из IWindowConfigurationService.
    /// </summary>
    public class CodeEditorFactory : ICodeEditorFactory
    {
        private readonly IWindowConfigurationService windowConfigurationService;

        /// <summary>
        /// Создаёт экземпляр фабрики.
        /// </summary>
        /// <param name="windowConfigurationService">Сервис настроек окна (шрифт, тема).</param>
        public CodeEditorFactory(IWindowConfigurationService windowConfigurationService)
        {
            this.windowConfigurationService = windowConfigurationService
                ?? throw new System.ArgumentNullException(nameof(windowConfigurationService));
        }

        /// <inheritdoc />
        public TextEditor Create(string content, string programmingLanguage)
        {
            var settings = windowConfigurationService.Settings;
            var fontSize = settings.FontSize > 0 ? settings.FontSize : 14.0;
            var fontFamily = !string.IsNullOrEmpty(settings.FontFamily)
                ? new FontFamily(settings.FontFamily)
                : new FontFamily("Consolas");

            var syntaxHighlighting = HighlightingManager.Instance.GetDefinition(programmingLanguage);

            var codeEditor = new TextEditor
            {
                ShowLineNumbers = true,
                WordWrap = true,
                Text = content ?? string.Empty,
                FontSize = fontSize,
                FontFamily = fontFamily,
                SyntaxHighlighting = syntaxHighlighting,
            };

            try
            {
                codeEditor.Background = (Brush)Application.Current.FindResource("EditorBackgroundBrush");
                codeEditor.Foreground = (Brush)Application.Current.FindResource("EditorForegroundBrush");
            }
            catch
            {
                // Ресурсы темы могут быть ещё не загружены
            }

            return codeEditor;
        }
    }
}

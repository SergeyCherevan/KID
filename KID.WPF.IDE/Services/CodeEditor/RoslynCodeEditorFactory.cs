using System.IO;
using Microsoft.CodeAnalysis;
using ICSharpCode.AvalonEdit;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.Initialize.Interfaces;
using RoslynPad.Editor;
using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Фабрика редакторов кода на базе RoslynCodeEditor (RoslynPad): IntelliSense, подсветка через Roslyn.
    /// </summary>
    public class RoslynCodeEditorFactory : ICodeEditorFactory
    {
        private readonly IRoslynHostService _roslynHostService;
        private readonly IWindowConfigurationService _windowConfigurationService;

        /// <summary>
        /// Создаёт экземпляр фабрики.
        /// </summary>
        /// <param name="roslynHostService">Сервис RoslynHost с настройками ссылок и импортов.</param>
        /// <param name="windowConfigurationService">Сервис настроек окна (шрифт, тема).</param>
        public RoslynCodeEditorFactory(
            IRoslynHostService roslynHostService,
            IWindowConfigurationService windowConfigurationService)
        {
            _roslynHostService = roslynHostService ?? throw new System.ArgumentNullException(nameof(roslynHostService));
            _windowConfigurationService = windowConfigurationService ?? throw new System.ArgumentNullException(nameof(windowConfigurationService));
        }

        /// <inheritdoc />
        public TextEditor Create(string content, string programmingLanguage)
        {
            var workingDirectory = Directory.GetCurrentDirectory();
            var roslynHost = _roslynHostService.GetHost();
            var editor = new RoslynCodeEditor();
#pragma warning disable VSTHRD002 // Синхронное ожидание: ICodeEditorFactory.Create синхронный; при необходимости вынести инициализацию в асинхронный сценарий.
            _ = editor.InitializeAsync(
                    roslynHost,
                    new ClassificationHighlightColors(),
                    workingDirectory,
                    content ?? string.Empty,
                    SourceCodeKind.Regular)
                .GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002

            editor.ShowLineNumbers = true;
            editor.WordWrap = true;

            return editor;
        }
    }
}

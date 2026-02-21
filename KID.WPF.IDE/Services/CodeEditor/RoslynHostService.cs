using System.Reflection;
using KID.Services.CodeEditor.Interfaces;
using NAudio.Wave;
using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Реализация сервиса RoslynHost с настройками для IDE KID: ссылки на KID.Library и NAudio, импорты System и KID.
    /// </summary>
    public class RoslynHostService : IRoslynHostService
    {
        private RoslynHost? _host;

        /// <inheritdoc />
        public RoslynHost GetHost()
        {
            if (_host != null)
                return _host;

            var references = RoslynHostReferences.NamespaceDefault.With(
                typeNamespaceImports: new[]
                {
                    typeof(object),
                    typeof(PlaybackState),
                    typeof(KID.Graphics)
                });

            // Не добавлять typeof(RoslynHost).Assembly — хост уже включает её в состав по умолчанию; дублирование даёт CompositionFailedException (два экспорта DocumentationProviderService).
            _host = new RoslynHost(
                additionalAssemblies: new[]
                {
                    Assembly.Load("RoslynPad.Roslyn.Windows"),
                    Assembly.Load("RoslynPad.Editor.Windows")
                },
                references: references);

            return _host;
        }
    }
}

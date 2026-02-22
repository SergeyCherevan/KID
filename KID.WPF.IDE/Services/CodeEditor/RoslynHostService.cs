using System;
using System.Reflection;
using KID.Services.CodeEditor.Interfaces;
using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Реализация сервиса RoslynHost: создаёт и кэширует хост с набором ссылок и импортов из <see cref="IRoslynReferenceProvider"/>.
    /// </summary>
    public class RoslynHostService : IRoslynHostService
    {
        private readonly IRoslynReferenceProvider _referenceProvider;
        private RoslynHost? _host;

        /// <summary>
        /// Создаёт экземпляр сервиса.
        /// </summary>
        /// <param name="referenceProvider">Провайдер сборок и типов для импортов (тот же источник, что и при выполнении кода в IDE).</param>
        public RoslynHostService(IRoslynReferenceProvider referenceProvider)
        {
            _referenceProvider = referenceProvider ?? throw new ArgumentNullException(nameof(referenceProvider));
        }

        /// <inheritdoc />
        public RoslynHost GetHost()
        {
            if (_host != null)
                return _host;

            var assemblies = _referenceProvider.GetAssemblies();
            var typeNamespaceImports = _referenceProvider.GetTypeNamespaceImports();

            var references = RoslynHostReferences.NamespaceDefault.With(
                assemblyReferences: assemblies,
                typeNamespaceImports: typeNamespaceImports);

            // additionalAssemblies — только для MEF редактора (RoslynPad). Не добавлять typeof(RoslynHost).Assembly — дублирование даёт CompositionFailedException (два экспорта DocumentationProviderService).
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

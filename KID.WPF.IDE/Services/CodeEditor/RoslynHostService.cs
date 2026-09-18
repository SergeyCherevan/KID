using System;
using System.Reflection;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.CompilationProfile.Interfaces;
using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Создаёт один RoslynHost с тем же immutable compilation profile, который использует compiler.
    /// </summary>
    internal sealed class RoslynHostService : IRoslynHostService
    {
        private readonly IKIDCompilationProfileProvider compilationProfileProvider;
        private readonly Lazy<RoslynHost> host;

        /// <summary>
        /// Создаёт экземпляр сервиса.
        /// </summary>
        /// <param name="compilationProfileProvider">
        /// Singleton-провайдер детерминированных references и явных imports.
        /// </param>
        public RoslynHostService(IKIDCompilationProfileProvider compilationProfileProvider)
        {
            this.compilationProfileProvider = compilationProfileProvider ??
                throw new ArgumentNullException(nameof(compilationProfileProvider));
            host = new Lazy<RoslynHost>(
                CreateHost,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <inheritdoc />
        public RoslynHost GetHost() => host.Value;

        private RoslynHost CreateHost()
        {
            var profile = compilationProfileProvider.GetProfile();

            // Empty исключает скрытые RoslynPad defaults. Editor получает только явные данные
            // профиля и потому не принимает код, который CSharpCompiler затем отвергнет.
            var references = RoslynHostReferences.Empty.With(
                references: profile.MetadataReferences,
                imports: profile.GlobalImports);

            // additionalAssemblies — только для MEF редактора (RoslynPad). Не добавлять typeof(RoslynHost).Assembly — дублирование даёт CompositionFailedException (два экспорта DocumentationProviderService).
            return new RoslynHost(
                additionalAssemblies: new[]
                {
                    Assembly.Load("RoslynPad.Roslyn.Windows"),
                    Assembly.Load("RoslynPad.Editor.Windows")
                },
                references: references);

        }
    }
}

using KID.Services.CodeExecution.Interfaces;
using KID.Services.CodeExecution.Rewriters;
using KID.Services.Localization.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NAudio.Wave;
using System.IO;
using System.Reflection;

namespace KID.Services.CodeExecution
{
    public class CSharpCompiler : ICodeCompiler
    {
        private readonly ILocalizationService _localizationService;

        public CSharpCompiler(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public async Task<CompilationResult> CompileAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(code);

            return await Task.Run(
                () => Compile(code, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        private CompilationResult Compile(string code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syntaxTree = CSharpSyntaxTree.ParseText(
                code,
                cancellationToken: cancellationToken);
            var references = CreateMetadataReferences(cancellationToken);

            var compilation = CSharpCompilation.Create(
                "UserProgram",
                [syntaxTree],
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            // Инструментирование отмены использует семантическую модель исходного дерева,
            // чтобы одноимённые пользовательские методы не переписывались как вызовы BCL.
            var semanticModel = compilation.GetSemanticModel(
                syntaxTree,
                ignoreAccessibility: true);
            var originalRoot = syntaxTree.GetRoot(cancellationToken);
            var cancellationRoot = new CancellationInstrumentationRewriter(
                semanticModel,
                cancellationToken).Visit(originalRoot) ?? originalRoot;
            var cancellationTree = syntaxTree.WithRootAndOptions(
                cancellationRoot,
                syntaxTree.Options);

            compilation = compilation.ReplaceSyntaxTree(syntaxTree, cancellationTree);
            cancellationToken.ThrowIfCancellationRequested();

            // Console.Clear обрабатывается отдельным семантическим преобразованием.
            // Новая семантическая модель после инструментации отмены гарантирует,
            // что каждый запрашиваемый узел принадлежит своему синтаксическому дереву.
            var consoleSemanticModel = compilation.GetSemanticModel(
                cancellationTree,
                ignoreAccessibility: true);
            var consoleRoot = cancellationTree.GetRoot(cancellationToken);
            var rewrittenRoot = new ConsoleClearRewriter(
                consoleSemanticModel,
                cancellationToken).Visit(consoleRoot) ?? consoleRoot;
            var rewrittenTree = cancellationTree.WithRootAndOptions(
                rewrittenRoot,
                cancellationTree.Options);

            compilation = compilation.ReplaceSyntaxTree(cancellationTree, rewrittenTree);
            cancellationToken.ThrowIfCancellationRequested();

            using var assemblyStream = new MemoryStream();
            var emitResult = compilation.Emit(
                assemblyStream,
                cancellationToken: cancellationToken);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var lineSpan = diagnostic.Location.GetLineSpan();
                        var line = lineSpan.StartLinePosition.Line + 1;
                        var message = diagnostic.GetMessage();
                        return _localizationService.GetString(
                            "Error_Compilation",
                            line,
                            message);
                    })
                    .ToList();

                return new CompilationResult
                {
                    Success = false,
                    Errors = errors
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            assemblyStream.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(assemblyStream.ToArray());
            cancellationToken.ThrowIfCancellationRequested();

            return new CompilationResult
            {
                Success = true,
                Assembly = assembly
            };
        }

        private static List<MetadataReference> CreateMetadataReferences(
            CancellationToken cancellationToken)
        {
            var references = new List<MetadataReference>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            // Инструментированные программы всегда ссылаются на StopManager, даже если
            // исходный код не использовал KID.Library и CLR ещё не загрузила эту сборку.
            AddReferenceIfMissing(
                references,
                typeof(global::KID.StopManager).Assembly.Location);

            // Инструментирование Console.Clear обращается к публичному мосту WPF-консоли
            // в этой сборке, поэтому зависимость также фиксируется явно независимо от порядка загрузки.
            AddReferenceIfMissing(
                references,
                typeof(CSharpCompiler).Assembly.Location);

            // Текущий запуск мог ещё не обращаться к NAudio, но публичные музыкальные API
            // KID используют PlaybackState и поэтому требуют явной ссылки на эту сборку.
            var naudioPath = typeof(PlaybackState).Assembly.Location;
            if (!string.IsNullOrEmpty(naudioPath))
                AddReferenceIfMissing(references, naudioPath);

            return references;
        }

        private static void AddReferenceIfMissing(
            ICollection<MetadataReference> references,
            string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                return;

            var alreadyAdded = references
                .OfType<PortableExecutableReference>()
                .Any(reference => string.Equals(
                    reference.FilePath,
                    assemblyPath,
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyAdded)
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
        }
    }
}

using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KID.Services.CompilationProfile
{
    /// <summary>
    /// Неизменяемый контракт compile-time окружения пользовательской программы.
    /// </summary>
    /// <remarks>
    /// Профиль хранит уже созданные Roslyn references и явный набор global imports. Consumers
    /// не дополняют эти коллекции локально, поэтому редактор и фактическая компиляция разрешают
    /// одинаковые assembly и пространства имён.
    /// </remarks>
    internal sealed class KIDCompilationProfile
    {
        /// <summary>
        /// Создаёт профиль и немедленно проверяет его инварианты.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// References или imports не заданы, содержат недопустимый элемент, дубликат либо
        /// references имеют недетерминированный порядок.
        /// </exception>
        internal KIDCompilationProfile(
            ImmutableArray<MetadataReference> metadataReferences,
            ImmutableArray<string> globalImports)
        {
            ValidateReferences(metadataReferences);
            ValidateImports(globalImports);

            MetadataReferences = metadataReferences;
            GlobalImports = globalImports;
        }

        /// <summary>
        /// Файловые metadata references в стабильном порядке без учёта регистра пути.
        /// </summary>
        internal ImmutableArray<MetadataReference> MetadataReferences { get; }

        /// <summary>
        /// Явные global imports, одинаково применяемые редактором и компилятором.
        /// </summary>
        internal ImmutableArray<string> GlobalImports { get; }

        private static void ValidateReferences(ImmutableArray<MetadataReference> references)
        {
            if (references.IsDefaultOrEmpty)
                throw new ArgumentException("Compilation profile must contain metadata references.", nameof(references));

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? previousPath = null;

            foreach (var reference in references)
            {
                if (reference is not PortableExecutableReference portableReference)
                    throw new ArgumentException("Every metadata reference must be a portable executable file reference.", nameof(references));

                var path = portableReference.FilePath;
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                    throw new ArgumentException("Every metadata reference must have an absolute file path.", nameof(references));

                var normalizedPath = Path.GetFullPath(path);
                if (!string.Equals(path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Metadata reference path is not normalized: '{path}'.", nameof(references));

                if (!File.Exists(normalizedPath))
                    throw new ArgumentException($"Metadata reference file does not exist: '{normalizedPath}'.", nameof(references));

                if (!paths.Add(normalizedPath))
                    throw new ArgumentException($"Duplicate metadata reference path: '{normalizedPath}'.", nameof(references));

                if (previousPath != null &&
                    StringComparer.OrdinalIgnoreCase.Compare(previousPath, normalizedPath) > 0)
                {
                    throw new ArgumentException("Metadata references must use stable ordinal-ignore-case ordering.", nameof(references));
                }

                previousPath = normalizedPath;
            }
        }

        private static void ValidateImports(ImmutableArray<string> imports)
        {
            if (imports.IsDefault)
                throw new ArgumentException("Global imports must be initialized.", nameof(imports));

            var uniqueImports = new HashSet<string>(StringComparer.Ordinal);
            foreach (var import in imports)
            {
                if (string.IsNullOrWhiteSpace(import) || !IsValidImport(import))
                    throw new ArgumentException($"Invalid global import: '{import}'.", nameof(imports));

                if (!uniqueImports.Add(import))
                    throw new ArgumentException($"Duplicate global import: '{import}'.", nameof(imports));
            }
        }

        private static bool IsValidImport(string import)
        {
            var compilationUnit = SyntaxFactory.ParseCompilationUnit($"using {import};");
            if (compilationUnit.ContainsDiagnostics ||
                compilationUnit.Usings.Count != 1 ||
                compilationUnit.Members.Count != 0)
            {
                return false;
            }

            var usingDirective = compilationUnit.Usings[0];
            return usingDirective.Alias == null &&
                   usingDirective.StaticKeyword.IsKind(SyntaxKind.None) &&
                   string.Equals(usingDirective.Name?.ToString(), import, StringComparison.Ordinal);
        }
    }
}

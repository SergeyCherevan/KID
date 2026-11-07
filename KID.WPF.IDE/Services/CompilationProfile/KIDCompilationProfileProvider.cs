using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using KID.Services.CompilationProfile.Interfaces;
using Microsoft.CodeAnalysis;
using NAudio.Wave;

namespace KID.Services.CompilationProfile
{
    /// <summary>
    /// Один раз строит детерминированный compilation profile из двух runtime framework packs
    /// и явно разрешённых прикладных assembly.
    /// </summary>
    /// <remarks>
    /// Провайдер намеренно не читает загруженные assembly текущего AppDomain. Состав профиля
    /// не зависит от порядка запуска редактора, компилятора или ранее выполненного кода.
    /// </remarks>
    internal sealed class KIDCompilationProfileProvider : IKIDCompilationProfileProvider
    {
        private const string TrustedPlatformAssembliesKey = "TRUSTED_PLATFORM_ASSEMBLIES";
        private readonly KIDCompilationProfile profile;

        /// <summary>
        /// Полностью строит и проверяет профиль до публикации провайдера через DI.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Текущий runtime не позволяет однозначно выделить framework assemblies либо отсутствует
        /// обязательная managed assembly.
        /// </exception>
        public KIDCompilationProfileProvider()
        {
            profile = CreateProfile();
        }

        /// <inheritdoc />
        public KIDCompilationProfile GetProfile() => profile;

        private static KIDCompilationProfile CreateProfile()
        {
            var referencePaths = new HashSet<string>(FrameworkReferenceResolver.Resolve(
                AppContext.BaseDirectory,
                typeof(object).Assembly.Location,
                typeof(System.Windows.Application).Assembly.Location,
                AppContext.GetData(TrustedPlatformAssembliesKey) as string), StringComparer.OrdinalIgnoreCase);

            AddRequiredReference(
                referencePaths,
                typeof(global::KID.Graphics).Assembly.Location,
                "KID.Library");
            AddRequiredReference(
                referencePaths,
                typeof(PlaybackState).Assembly.Location,
                "NAudio.Core");

            var orderedPaths = referencePaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            ValidateAssemblyIdentities(orderedPaths);
            ValidateRequiredAssemblies(
                orderedPaths,
                typeof(global::KID.Graphics).Assembly.GetName().Name,
                typeof(PlaybackState).Assembly.GetName().Name);

            var references = orderedPaths
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToImmutableArray();

            return new KIDCompilationProfile(
                references,
                ImmutableArray<string>.Empty);
        }

        private static bool HasManagedMetadata(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var peReader = new PEReader(stream);
                return peReader.HasMetadata;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException($"Cannot inspect runtime reference '{path}'.", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new InvalidOperationException($"Cannot inspect runtime reference '{path}'.", exception);
            }
        }

        private static void AddRequiredReference(
            ISet<string> referencePaths,
            string path,
            string description)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException($"Required assembly {description} has no file location.");

            var normalizedPath = Path.GetFullPath(path);
            if (!File.Exists(normalizedPath))
                throw new InvalidOperationException($"Required assembly {description} does not exist at '{normalizedPath}'.");
            if (!HasManagedMetadata(normalizedPath))
                throw new InvalidOperationException($"Required assembly {description} has no managed metadata: '{normalizedPath}'.");

            referencePaths.Add(normalizedPath);
        }

        private static void ValidateAssemblyIdentities(IEnumerable<string> paths)
        {
            var identities = new Dictionary<string, (AssemblyName Identity, string Path)>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                AssemblyName identity;
                try
                {
                    identity = AssemblyName.GetAssemblyName(path);
                }
                catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or IOException)
                {
                    throw new InvalidOperationException($"Cannot read assembly identity from '{path}'.", exception);
                }

                if (string.IsNullOrWhiteSpace(identity.Name))
                    throw new InvalidOperationException($"Assembly at '{path}' has no simple identity name.");

                if (identities.TryGetValue(identity.Name, out var existing) &&
                    !string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Conflicting assembly identity '{identity.Name}' was found at '{existing.Path}' and '{path}'.");
                }

                identities[identity.Name] = (identity, path);
            }
        }

        private static void ValidateRequiredAssemblies(
            IEnumerable<string> paths,
            string? kidLibraryName,
            string? playbackStateAssemblyName)
        {
            string?[] requiredNames =
            [
                "System.Private.CoreLib",
                "System.Runtime",
                "System.Console",
                kidLibraryName,
                playbackStateAssemblyName
            ];
            var actualNames = paths
                .Select(path => AssemblyName.GetAssemblyName(path).Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var requiredName in requiredNames)
            {
                if (string.IsNullOrWhiteSpace(requiredName) || !actualNames.Contains(requiredName))
                    throw new InvalidOperationException($"Required compilation reference '{requiredName ?? "<unknown>"}' is missing.");
            }
        }
    }
}

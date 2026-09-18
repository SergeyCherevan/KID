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
            var netCoreDirectory = GetAssemblyDirectory(
                typeof(object).Assembly,
                "Microsoft.NETCore.App");
            var windowsDesktopDirectory = GetAssemblyDirectory(
                typeof(System.Windows.Application).Assembly,
                "Microsoft.WindowsDesktop.App");
            var applicationDirectory = NormalizeDirectory(AppContext.BaseDirectory);

            if (AreSameDirectory(netCoreDirectory, applicationDirectory) ||
                AreSameDirectory(windowsDesktopDirectory, applicationDirectory))
            {
                throw new InvalidOperationException(
                    "Self-contained layout is not supported because framework and app-local assemblies share AppContext.BaseDirectory.");
            }

            var trustedPlatformAssemblies = AppContext.GetData(TrustedPlatformAssembliesKey) as string;
            if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            {
                throw new InvalidOperationException(
                    $"AppContext data '{TrustedPlatformAssembliesKey}' is missing or empty.");
            }

            var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var netCoreReferenceCount = 0;
            var windowsDesktopReferenceCount = 0;

            foreach (var candidate in trustedPlatformAssemblies.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var path = Path.GetFullPath(candidate);
                var isNetCoreReference = IsPathWithinDirectory(path, netCoreDirectory);
                var isWindowsDesktopReference = IsPathWithinDirectory(path, windowsDesktopDirectory);
                if (!isNetCoreReference && !isWindowsDesktopReference)
                    continue;

                if (!File.Exists(path))
                    throw new InvalidOperationException($"Runtime reference file does not exist: '{path}'.");

                if (!HasManagedMetadata(path) || !referencePaths.Add(path))
                    continue;

                if (isNetCoreReference)
                    netCoreReferenceCount++;
                if (isWindowsDesktopReference)
                    windowsDesktopReferenceCount++;
            }

            if (netCoreReferenceCount == 0)
                throw new InvalidOperationException($"No managed references were found in Microsoft.NETCore.App directory '{netCoreDirectory}'.");
            if (windowsDesktopReferenceCount == 0)
                throw new InvalidOperationException($"No managed references were found in Microsoft.WindowsDesktop.App directory '{windowsDesktopDirectory}'.");

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

        private static string GetAssemblyDirectory(Assembly assembly, string frameworkName)
        {
            if (string.IsNullOrWhiteSpace(assembly.Location))
                throw new InvalidOperationException($"Cannot resolve {frameworkName}: assembly '{assembly.FullName}' has no file location.");

            var directory = Path.GetDirectoryName(Path.GetFullPath(assembly.Location));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new InvalidOperationException($"Cannot resolve {frameworkName} directory from '{assembly.Location}'.");

            return NormalizeDirectory(directory);
        }

        private static string NormalizeDirectory(string directory) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        private static bool AreSameDirectory(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static bool IsPathWithinDirectory(string path, string directory)
        {
            var directoryPrefix = directory + Path.DirectorySeparatorChar;
            return path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
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
            var requiredNames = new[]
            {
                "System.Private.CoreLib",
                "System.Runtime",
                "System.Console",
                kidLibraryName,
                playbackStateAssemblyName
            };
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

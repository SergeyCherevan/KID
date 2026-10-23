using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Serialization;
using KID.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KID.Services.Localization;

internal sealed record LanguageDefinition(string CultureCode, string DisplayNameKey);

internal sealed class LanguageCatalog
{
    internal const string ResourceName = "KID.Resources.AvailableLanguages.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    private LanguageCatalog(ImmutableArray<LanguageDefinition> languages, bool isFallback)
    {
        Languages = languages;
        LanguageKeys = languages.Select(language => language.DisplayNameKey).ToImmutableArray();
        KeyToCulture = languages.ToFrozenDictionary(
            language => language.DisplayNameKey,
            language => language.CultureCode,
            StringComparer.OrdinalIgnoreCase);
        CultureToKey = languages.ToFrozenDictionary(
            language => language.CultureCode,
            language => language.DisplayNameKey,
            StringComparer.OrdinalIgnoreCase);
        IsFallback = isFallback;
    }

    internal ImmutableArray<LanguageDefinition> Languages { get; }
    internal ImmutableArray<string> LanguageKeys { get; }
    internal FrozenDictionary<string, string> KeyToCulture { get; }
    internal FrozenDictionary<string, string> CultureToKey { get; }
    internal bool IsFallback { get; }

    internal static Lazy<LanguageCatalog> CreateLazy(
        Func<Stream?> openStream,
        Func<CultureInfo, ResourceSet?> getResourceSet,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(openStream);
        ArgumentNullException.ThrowIfNull(getResourceSet);

        // ExecutionAndPublication also caches the deliberate fallback, so a broken
        // embedded resource is read and diagnosed only once per service instance.
        return new Lazy<LanguageCatalog>(() => Load(openStream, getResourceSet, logger));
    }

    private static LanguageCatalog Load(
        Func<Stream?> openStream,
        Func<CultureInfo, ResourceSet?> getResourceSet,
        ILogger? logger)
    {
        try
        {
            using var stream = openStream()
                ?? throw new InvalidDataException("The embedded catalog stream is missing.");
            var entries = JsonSerializer.Deserialize<CatalogEntry?[]>(stream, JsonOptions);
            if (entries is null || entries.Length == 0)
                throw new InvalidDataException("The catalog must be a non-empty array.");

            var languages = ImmutableArray.CreateBuilder<LanguageDefinition>(entries.Length);
            var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index]
                    ?? throw InvalidEntry(index, "entry", null, "must be an object");
                ValidateValue(entry.CultureCode, index, "cultureCode");
                ValidateValue(entry.DisplayNameKey, index, "displayNameKey");

                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(entry.CultureCode);
                }
                catch (CultureNotFoundException exception)
                {
                    throw new InvalidDataException(
                        InvalidEntry(index, "cultureCode", entry.CultureCode, "is not recognized").Message,
                        exception);
                }

                if (!cultures.Add(culture.Name))
                    throw InvalidEntry(index, "cultureCode", entry.CultureCode, "duplicates a culture");
                if (!keys.Add(entry.DisplayNameKey))
                    throw InvalidEntry(index, "displayNameKey", entry.DisplayNameKey, "duplicates a key");

                languages.Add(new LanguageDefinition(culture.Name, entry.DisplayNameKey));
            }

            var definitions = languages.MoveToImmutable();
            for (var index = 0; index < definitions.Length; index++)
            {
                var language = definitions[index];
                var resourceSet = getResourceSet(CultureInfo.GetCultureInfo(language.CultureCode))
                    ?? throw InvalidEntry(index, "cultureCode", language.CultureCode,
                        "has no own Strings resource set");

                // ResourceManager can already have a parent/fallback set cached.
                // Its metadata must still identify exactly the requested culture.
                if (resourceSet.GetObject("Metadata_CultureCode", ignoreCase: false) is not string metadata
                    || !string.Equals(metadata, language.CultureCode, StringComparison.OrdinalIgnoreCase))
                {
                    throw InvalidEntry(index, "cultureCode", language.CultureCode,
                        "does not match Metadata_CultureCode in its Strings resource set");
                }

                for (var keyIndex = 0; keyIndex < definitions.Length; keyIndex++)
                {
                    var key = definitions[keyIndex].DisplayNameKey;
                    if (resourceSet.GetObject(key, ignoreCase: false) is not string text
                        || string.IsNullOrWhiteSpace(text))
                    {
                        throw InvalidEntry(keyIndex, "displayNameKey", key,
                            $"has no non-empty exact-case string in Strings for '{language.CultureCode}'");
                    }
                }

                // Resource sets belong to the shared ResourceManager; do not dispose them.
            }

            return new LanguageCatalog(definitions, isFallback: false);
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or MissingManifestResourceException
            or MissingSatelliteAssemblyException)
        {
            logger?.LogWarning(
                DiagnosticEventIds.LocalizationFallback,
                exception,
                "Language catalog {ResourceName} could not be loaded. Reason={Reason}",
                ResourceName,
                exception.Message);

            return new LanguageCatalog(
                [new LanguageDefinition("en-US", "Language_English")],
                isFallback: true);
        }
    }

    private static void ValidateValue(string? value, int index, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw InvalidEntry(index, field, value, "must be non-empty with no surrounding whitespace");
    }

    private static InvalidDataException InvalidEntry(int index, string field, string? value, string reason) =>
        new($"Entry[{index}].{field}='{value ?? "null"}' {reason}.");

    private sealed class CatalogEntry
    {
        [JsonPropertyName("cultureCode")]
        public required string CultureCode { get; init; }

        [JsonPropertyName("displayNameKey")]
        public required string DisplayNameKey { get; init; }
    }
}

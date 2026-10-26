using System.Collections.Immutable;
using System.IO;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Serialization;
using KID.Models;
using KID.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KID.Services.Themes;

/// <summary>
/// Проверенный каталог встроенных тем; загрузка XAML остаётся ответственностью ThemeService.
/// </summary>
internal sealed class ThemeCatalog
{
    internal const string ResourceName = "KID.Resources.AvailableThemes.json";
    internal static ThemeDefinition DefaultTheme { get; } =
        new("Theme_VS2022Light", "Themes/VS2022LightTheme.xaml");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    private ThemeCatalog(ImmutableArray<ThemeDefinition> themes, bool isFallback)
    {
        Themes = themes;
        IsFallback = isFallback;
    }

    internal ImmutableArray<ThemeDefinition> Themes { get; }
    internal bool IsFallback { get; }

    internal static Lazy<ThemeCatalog> CreateLazy(Func<Stream?> openStream, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(openStream);
        return new Lazy<ThemeCatalog>(() => Load(openStream, logger));
    }

    private static ThemeCatalog Load(Func<Stream?> openStream, ILogger? logger)
    {
        try
        {
            using var stream = openStream()
                ?? throw new InvalidDataException("The embedded catalog stream is missing.");
            var entries = JsonSerializer.Deserialize<CatalogEntry?[]>(stream, JsonOptions);
            if (entries is not { Length: > 0 })
                throw new InvalidDataException("The catalog must be a non-empty array.");

            var themes = ImmutableArray.CreateBuilder<ThemeDefinition>(entries.Length);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index]
                    ?? throw new InvalidDataException($"Entry[{index}] must be an object.");
                ValidateValue(entry.LocalizationKey, index, "localizationKey");
                ValidateValue(entry.ResourcePath, index, "resourcePath");
                if (!keys.Add(entry.LocalizationKey))
                    throw InvalidEntry(index, "localizationKey", entry.LocalizationKey, "duplicates a key");
                if (!string.Equals(Path.GetExtension(entry.ResourcePath), ".xaml", StringComparison.OrdinalIgnoreCase)
                    || !Uri.TryCreate(entry.ResourcePath, UriKind.Relative, out _))
                {
                    throw InvalidEntry(index, "resourcePath", entry.ResourcePath, "must be a relative XAML URI");
                }
                if (entry.LocalizationKey == DefaultTheme.LocalizationKey && entry.ResourcePath != DefaultTheme.ResourcePath)
                    throw InvalidEntry(index, "resourcePath", entry.ResourcePath, "must retain the built-in default theme path");

                themes.Add(new ThemeDefinition(entry.LocalizationKey, entry.ResourcePath));
            }

            if (!keys.Contains(DefaultTheme.LocalizationKey))
                throw new InvalidDataException($"The catalog must contain '{DefaultTheme.LocalizationKey}'.");

            return new ThemeCatalog(themes.MoveToImmutable(), isFallback: false);
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidDataException or IOException or UnauthorizedAccessException or MissingManifestResourceException)
        {
            logger?.LogWarning(
                DiagnosticEventIds.SettingsFallback,
                exception,
                "Theme catalog {ResourceName} could not be loaded. Reason={Reason}",
                ResourceName,
                exception.Message);
            return new ThemeCatalog([DefaultTheme], isFallback: true);
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
        [JsonPropertyName("localizationKey")]
        public required string LocalizationKey { get; init; }

        [JsonPropertyName("resourcePath")]
        public required string ResourcePath { get; init; }
    }
}

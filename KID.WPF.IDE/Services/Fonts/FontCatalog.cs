using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Serialization;
using KID.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KID.Services.Fonts;

/// <summary>
/// Неизменяемый каталог шрифтов и размеров, независимый от установленных шрифтов Windows.
/// </summary>
internal sealed class FontCatalog
{
    internal const string ResourceName = "KID.Resources.AvailableFontsAndSizes.json";
    internal static ImmutableArray<string> DefaultFonts { get; } = ["Consolas"];
    internal static ImmutableArray<double> DefaultFontSizes { get; } = [10, 11, 12, 13, 14, 16, 18, 20];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    private FontCatalog(ImmutableArray<string> fonts, ImmutableArray<double> fontSizes, bool isFallback)
    {
        Fonts = fonts;
        FontSizes = fontSizes;
        IsFallback = isFallback;
    }

    internal ImmutableArray<string> Fonts { get; }
    internal ImmutableArray<double> FontSizes { get; }
    internal bool IsFallback { get; }

    internal static Lazy<FontCatalog> CreateLazy(Func<Stream?> openStream, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(openStream);
        // Cache both the valid snapshot and deliberate fallback once per provider instance.
        return new Lazy<FontCatalog>(() => Load(openStream, logger));
    }

    private static FontCatalog Load(Func<Stream?> openStream, ILogger? logger)
    {
        try
        {
            using var stream = openStream()
                ?? throw new InvalidDataException("The embedded catalog stream is missing.");
            var data = JsonSerializer.Deserialize<CatalogData>(stream, JsonOptions);
            if (data?.Fonts is not { Length: > 0 } || data.FontSizes is not { Length: > 0 })
                throw new InvalidDataException("The catalog must contain non-empty fonts and fontSizes arrays.");

            var fonts = ImmutableArray.CreateBuilder<string>(data.Fonts.Length);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < data.Fonts.Length; index++)
            {
                var font = data.Fonts[index];
                if (string.IsNullOrWhiteSpace(font) || font != font.Trim())
                    throw new InvalidDataException($"fonts[{index}]='{font ?? "null"}' must be non-empty with no surrounding whitespace.");
                if (!names.Add(font))
                    throw new InvalidDataException($"fonts[{index}]='{font}' duplicates a font name.");
                fonts.Add(font);
            }

            var sizes = new HashSet<double>();
            for (var index = 0; index < data.FontSizes.Length; index++)
            {
                var size = data.FontSizes[index];
                if (!double.IsFinite(size) || size <= 0)
                    throw new InvalidDataException($"fontSizes[{index}]='{size.ToString(CultureInfo.InvariantCulture)}' must be finite and positive.");
                if (!sizes.Add(size))
                    throw new InvalidDataException($"fontSizes[{index}]='{size.ToString(CultureInfo.InvariantCulture)}' duplicates a size.");
            }

            return new FontCatalog(fonts.MoveToImmutable(), data.FontSizes.ToImmutableArray(), isFallback: false);
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidDataException or IOException or UnauthorizedAccessException or MissingManifestResourceException)
        {
            logger?.LogWarning(
                DiagnosticEventIds.SettingsFallback,
                exception,
                "Font catalog {ResourceName} could not be loaded. Reason={Reason}",
                ResourceName,
                exception.Message);
            return new FontCatalog(DefaultFonts, DefaultFontSizes, isFallback: true);
        }
    }

    private sealed class CatalogData
    {
        [JsonPropertyName("fonts")]
        public required string?[]? Fonts { get; init; }

        [JsonPropertyName("fontSizes")]
        public required double[]? FontSizes { get; init; }
    }
}

using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using KID.Services.Diagnostics;
using KID.Services.Fonts.Interfaces;
using Microsoft.Extensions.Logging;

namespace KID.Services.Fonts;

/// <summary>
/// Предоставляет установленные шрифты и размеры из встроенного JSON-каталога.
/// </summary>
public class FontProviderService : IFontProviderService
{
    private readonly Lazy<FontCatalog> catalog;
    private readonly Func<IEnumerable<string>> getInstalledFontNames;
    private readonly ILogger? logger;

    public FontProviderService(ILogger<FontProviderService>? logger = null)
        : this(
            () => typeof(FontProviderService).Assembly.GetManifestResourceStream(FontCatalog.ResourceName),
            () => System.Windows.Media.Fonts.SystemFontFamilies.Select(font => font.Source),
            logger)
    {
    }

    internal FontProviderService(Func<Stream?> openStream, Func<IEnumerable<string>> getInstalledFontNames, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(getInstalledFontNames);
        catalog = FontCatalog.CreateLazy(openStream, logger);
        this.getInstalledFontNames = getInstalledFontNames;
        this.logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableFonts()
    {
        var snapshot = catalog.Value;
        if (snapshot.IsFallback)
            return snapshot.Fonts;

        try
        {
            return FilterInstalledFonts(snapshot.Fonts, getInstalledFontNames());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ExternalException or InvalidOperationException or NotSupportedException or FormatException)
        {
            logger?.LogWarning(
                DiagnosticEventIds.SettingsFallback,
                exception,
                "Installed fonts could not be enumerated; using the default font.");
            return FontCatalog.DefaultFonts;
        }
    }

    /// <inheritdoc />
    public IEnumerable<double> GetAvailableFontSizes() => catalog.Value.FontSizes;

    internal static ImmutableArray<string> FilterInstalledFonts(
        ImmutableArray<string> fonts, IEnumerable<string> installedFontNames)
    {
        var installed = installedFontNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = fonts.Where(installed.Contains).ToImmutableArray();
        return result.IsEmpty ? FontCatalog.DefaultFonts : result;
    }
}

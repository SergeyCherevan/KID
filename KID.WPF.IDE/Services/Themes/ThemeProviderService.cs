using System.IO;
using KID.Models;
using KID.Services.Themes.Interfaces;
using Microsoft.Extensions.Logging;

namespace KID.Services.Themes;

/// <summary>
/// Предоставляет темы из встроенного JSON-каталога приложения.
/// </summary>
public sealed class ThemeProviderService : IThemeProviderService
{
    private readonly Lazy<ThemeCatalog> catalog;

    public ThemeProviderService(ILogger<ThemeProviderService>? logger = null)
        : this(() => typeof(ThemeProviderService).Assembly.GetManifestResourceStream(ThemeCatalog.ResourceName), logger)
    {
    }

    internal ThemeProviderService(Func<Stream?> openStream, ILogger? logger = null)
    {
        catalog = ThemeCatalog.CreateLazy(openStream, logger);
    }

    /// <inheritdoc />
    public IReadOnlyList<ThemeDefinition> GetAvailableThemes() => catalog.Value.Themes;

    /// <inheritdoc />
    public ThemeDefinition GetDefaultTheme() =>
        TryGetTheme(ThemeCatalog.DefaultTheme.LocalizationKey, out var theme)
            ? theme
            : ThemeCatalog.DefaultTheme;

    /// <inheritdoc />
    public bool TryGetTheme(string? localizationKey, out ThemeDefinition theme)
    {
        // Старый settings.json продолжает выбирать ту же светлую/тёмную тему.
        // Возвращаем канонический ключ: ThemeService сохранит его после применения.
        localizationKey = localizationKey switch
        {
            "Theme_Light" => "Theme_VS2022Light",
            "Theme_Dark" => "Theme_VS2022Dark",
            _ => localizationKey
        };

        if (!string.IsNullOrWhiteSpace(localizationKey))
        {
            foreach (var availableTheme in catalog.Value.Themes)
            {
                if (string.Equals(availableTheme.LocalizationKey, localizationKey, StringComparison.Ordinal))
                {
                    theme = availableTheme;
                    return true;
                }
            }
        }

        theme = null!;
        return false;
    }
}

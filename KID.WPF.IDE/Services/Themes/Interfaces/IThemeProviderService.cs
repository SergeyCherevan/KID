using System.Collections.Generic;
using KID.Models;

namespace KID.Services.Themes.Interfaces;

/// <summary>
/// Предоставляет каталог доступных тем приложения.
/// </summary>
public interface IThemeProviderService
{
    IReadOnlyList<ThemeDefinition> GetAvailableThemes();

    ThemeDefinition GetDefaultTheme();

    bool TryGetTheme(string? localizationKey, out ThemeDefinition theme);
}

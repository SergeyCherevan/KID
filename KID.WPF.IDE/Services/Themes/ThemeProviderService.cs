using System;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using KID.Models;
using KID.Resources;
using KID.Services.Themes.Interfaces;

namespace KID.Services.Themes;

/// <summary>
/// Загружает каталог встроенных тем из нейтрального ресурса приложения.
/// </summary>
public sealed class ThemeProviderService : IThemeProviderService
{
    private static readonly ThemeDefinition BuiltInDefaultTheme =
        new("Theme_Light", "Themes/LightTheme.xaml");

    private static readonly ResourceManager ResourceManager =
        new("KID.Resources.AvailableThemes", typeof(Strings).Assembly);

    private readonly IReadOnlyList<ThemeDefinition> availableThemes;

    public ThemeProviderService()
    {
        availableThemes = LoadAvailableThemes();
    }

    /// <inheritdoc />
    public IReadOnlyList<ThemeDefinition> GetAvailableThemes() => availableThemes;

    /// <inheritdoc />
    public ThemeDefinition GetDefaultTheme() =>
        TryGetTheme(BuiltInDefaultTheme.LocalizationKey, out var theme)
            ? theme
            : BuiltInDefaultTheme;

    /// <inheritdoc />
    public bool TryGetTheme(string? localizationKey, out ThemeDefinition theme)
    {
        if (!string.IsNullOrWhiteSpace(localizationKey))
        {
            foreach (var availableTheme in availableThemes)
            {
                if (string.Equals(
                        availableTheme.LocalizationKey,
                        localizationKey,
                        StringComparison.Ordinal))
                {
                    theme = availableTheme;
                    return true;
                }
            }
        }

        theme = null!;
        return false;
    }

    private static IReadOnlyList<ThemeDefinition> LoadAvailableThemes()
    {
        try
        {
            var countText = ResourceManager.GetString("Theme_Count");
            if (!int.TryParse(countText, out var count) || count <= 0)
                return [BuiltInDefaultTheme];

            var result = new List<ThemeDefinition>(count);
            var addedKeys = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < count; index++)
            {
                var localizationKey = ResourceManager.GetString($"Theme_{index}_LocalizationKey");
                var resourcePath = ResourceManager.GetString($"Theme_{index}_ResourcePath");

                if (!IsValidDefinition(localizationKey, resourcePath) ||
                    !addedKeys.Add(localizationKey!))
                {
                    continue;
                }

                result.Add(new ThemeDefinition(localizationKey!, resourcePath!));
            }

            return result.Count > 0 ? result : [BuiltInDefaultTheme];
        }
        catch
        {
            return [BuiltInDefaultTheme];
        }
    }

    private static bool IsValidDefinition(string? localizationKey, string? resourcePath)
    {
        if (string.IsNullOrWhiteSpace(localizationKey) || string.IsNullOrWhiteSpace(resourcePath))
            return false;

        if (!string.Equals(Path.GetExtension(resourcePath), ".xaml", StringComparison.OrdinalIgnoreCase))
            return false;

        return Uri.TryCreate(resourcePath, UriKind.Relative, out _);
    }
}

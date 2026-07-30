namespace KID.Services.Themes;

/// <summary>
/// Преобразует значения темы из старых версий настроек в актуальные ключи локализации.
/// </summary>
internal static class LegacyThemeKeyMigrator
{
    public static string? Migrate(string? themeKey) =>
        themeKey switch
        {
            "Light" => "Theme_Light",
            "Dark" => "Theme_Dark",
            _ => themeKey
        };
}

using System.Globalization;
using System.Resources;
using KID.Models;
using KID.Services.Localization;
using KID.Services.Themes;
using KID.Tests.Infrastructure;

namespace KID.Tests.Themes;

public sealed class ThemeCatalogResourceTests
{
    [Fact]
    public void ShippedCatalog_LoadsExpectedThemesAndOwnTranslationsWithoutFallback()
    {
        var assembly = typeof(ThemeProviderService).Assembly;
        Assert.Contains(ThemeCatalog.ResourceName, assembly.GetManifestResourceNames());
        Assert.DoesNotContain("KID.Resources.AvailableThemes.resources", assembly.GetManifestResourceNames());
        var logger = new ResourceCatalogTestLogger();
        var catalog = ThemeCatalog.CreateLazy(() => assembly.GetManifestResourceStream(ThemeCatalog.ResourceName), logger).Value;
        Assert.False(catalog.IsFallback);
        Assert.Equal(new ThemeDefinition[]
        {
            new("Theme_VS2022Light", "Themes/VS2022LightTheme.xaml"),
            new("Theme_VS2022Dark", "Themes/VS2022DarkTheme.xaml"),
            new("Theme_VS2026Light", "Themes/VS2026LightTheme.xaml"),
            new("Theme_VS2026Dark", "Themes/VS2026DarkTheme.xaml")
        }, catalog.Themes);

        var names = new ResourceManager("KID.Resources.Strings", assembly);
        try
        {
            var languages = LanguageCatalog.CreateLazy(
                () => assembly.GetManifestResourceStream(LanguageCatalog.ResourceName),
                culture => names.GetResourceSet(culture, createIfNotExists: true, tryParents: false), logger).Value;
            Assert.False(languages.IsFallback);
            foreach (var language in languages.Languages)
            {
                var ownStrings = names.GetResourceSet(CultureInfo.GetCultureInfo(language.CultureCode),
                    createIfNotExists: true, tryParents: false);
                Assert.NotNull(ownStrings);
                Assert.Equal(language.CultureCode, ownStrings.GetString("Metadata_CultureCode", ignoreCase: false));
                foreach (var theme in catalog.Themes)
                    Assert.False(string.IsNullOrWhiteSpace(ownStrings.GetString(theme.LocalizationKey, ignoreCase: false)),
                        $"Missing '{theme.LocalizationKey}' in '{language.CultureCode}'.");
            }
            Assert.Empty(logger.Entries);
        }
        finally { names.ReleaseAllResources(); }
    }
}

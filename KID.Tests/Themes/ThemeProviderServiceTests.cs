using System.Globalization;
using System.Resources;
using KID.Models;
using KID.Services.Themes;

namespace KID.Tests.Themes;

public sealed class ThemeProviderServiceTests
{
    [Fact]
    public void Catalog_ContainsFourCanonicalThemesInMenuOrder()
    {
        var provider = new ThemeProviderService();
        Assert.Equal(
            ["Theme_VS2022Light", "Theme_VS2022Dark", "Theme_VS2026Light", "Theme_VS2026Dark"],
            provider.GetAvailableThemes().Select(theme => theme.LocalizationKey));
        Assert.Equal("Theme_VS2022Light", provider.GetDefaultTheme().LocalizationKey);
        Assert.Equal(provider.GetDefaultTheme().LocalizationKey, new WindowConfigurationData().ColorTheme);

        var names = new ResourceManager("KID.Resources.Strings", typeof(KID.App).Assembly);
        foreach (var theme in provider.GetAvailableThemes())
        {
            foreach (var culture in new[] { "en", "ru", "uk" })
            {
                var name = names.GetString(theme.LocalizationKey, CultureInfo.GetCultureInfo(culture));
                Assert.False(string.IsNullOrWhiteSpace(name));
                Assert.Contains(theme.LocalizationKey.Contains("2026", StringComparison.Ordinal) ? "2026" : "2022", name);
            }
        }
    }

    [Theory]
    [InlineData("Theme_Light", "Theme_VS2022Light", "Themes/VS2022LightTheme.xaml")]
    [InlineData("Theme_Dark", "Theme_VS2022Dark", "Themes/VS2022DarkTheme.xaml")]
    [InlineData("Theme_VS2022Light", "Theme_VS2022Light", "Themes/VS2022LightTheme.xaml")]
    [InlineData("Theme_VS2022Dark", "Theme_VS2022Dark", "Themes/VS2022DarkTheme.xaml")]
    [InlineData("Theme_VS2026Light", "Theme_VS2026Light", "Themes/VS2026LightTheme.xaml")]
    [InlineData("Theme_VS2026Dark", "Theme_VS2026Dark", "Themes/VS2026DarkTheme.xaml")]
    public void ResolveTheme_ReturnsCanonicalDefinition(string savedKey, string canonicalKey, string path)
    {
        var provider = new ThemeProviderService();
        Assert.True(provider.TryGetTheme(savedKey, out var theme));
        Assert.Equal(new ThemeDefinition(canonicalKey, path), theme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Theme_NotInstalled")]
    public void UnknownKey_IsNotAddedToCatalog(string? key)
    {
        var provider = new ThemeProviderService();
        Assert.False(provider.TryGetTheme(key, out _));
        Assert.Equal(4, provider.GetAvailableThemes().Count);
    }
}

using System.Globalization;
using System.Text.Json;
using KID.Models;
using KID.Services.Themes;
using KID.Tests.Infrastructure;
using static KID.Tests.Infrastructure.ResourceCatalogTestSupport;

namespace KID.Tests.Themes;

public sealed class ThemeCatalogTests
{
    internal const string DefaultEntry = """{"localizationKey":"Theme_VS2022Light","resourcePath":"Themes/VS2022LightTheme.xaml"}""";
    private const string ValidJson = "[" + DefaultEntry + "," +
        """{"localizationKey":"Theme_Custom","resourcePath":"Themes/Custom.XAML"}""" + "]";

    [Fact]
    public void ValidCatalog_IsLoadedOnceAndPublishesImmutableOrderedSnapshot()
    {
        var logger = new ResourceCatalogTestLogger();
        var reads = 0;
        var lazy = ThemeCatalog.CreateLazy(() =>
        {
            Interlocked.Increment(ref reads);
            return JsonStream(ValidJson);
        }, logger);
        Assert.Equal(0, reads);
        var snapshots = new ThemeCatalog[16];
        Parallel.For(0, snapshots.Length, index => snapshots[index] = lazy.Value);
        var catalog = lazy.Value;
        Assert.All(snapshots, snapshot => Assert.Same(catalog, snapshot));
        Assert.Equal(1, reads);
        Assert.Empty(logger.Entries);
        Assert.False(catalog.IsFallback);
        Assert.Equal(["Theme_VS2022Light", "Theme_Custom"], catalog.Themes.Select(theme => theme.LocalizationKey));
        Assert.Equal("Themes/Custom.XAML", catalog.Themes[1].ResourcePath);
        Assert.Throws<NotSupportedException>(() => ((IList<ThemeDefinition>)catalog.Themes).Clear());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[42]")]
    [InlineData("[{}]")]
    [InlineData("[{\"localizationKey\":\"Theme_Custom\"}]")]
    [InlineData("[{\"resourcePath\":\"Themes/Custom.xaml\"}]")]
    [InlineData("[{\"localizationKey\":42,\"resourcePath\":\"Themes/Custom.xaml\"}]")]
    [InlineData("[{\"localizationKey\":\"Theme_Custom\",\"resourcePath\":false}]")]
    [InlineData("[{\"LocalizationKey\":\"Theme_VS2022Light\",\"resourcePath\":\"Themes/VS2022LightTheme.xaml\"}]")]
    [InlineData("[" + DefaultEntry + ",]")]
    [InlineData("[/* comment */" + DefaultEntry + "]")]
    [InlineData("[{\"localizationKey\":\"Theme_VS2022Light\",\"resourcePath\":\"Themes/VS2022LightTheme.xaml\",\"extra\":1}]")]
    [InlineData("[{\"localizationKey\":\"Theme_VS2022Light\",\"localizationKey\":\"Theme_VS2022Light\",\"resourcePath\":\"Themes/VS2022LightTheme.xaml\"}]")]
    [InlineData("[{\"localizationKey\":\"Theme_VS2022Light\",\"resourcePath\":\"Themes/VS2022LightTheme.xaml\",\"resourcePath\":\"Themes/Custom.xaml\"}]")]
    [InlineData("[" + DefaultEntry + "," + DefaultEntry + "]")]
    [InlineData("[{\"localizationKey\":\"Theme_Custom\",\"resourcePath\":\"Themes/Custom.xaml\"}]")]
    [InlineData("[{\"localizationKey\":\"Theme_VS2022Light\",\"resourcePath\":\"Themes/Custom.xaml\"}]")]
    public void InvalidJsonOrDefaultContract_RejectsWholeCatalog(string json)
    {
        var logger = new ResourceCatalogTestLogger();
        AssertFallback(ThemeCatalog.CreateLazy(() => JsonStream(json), logger).Value, logger);
    }

    [Theory]
    [InlineData(null, "Themes/Custom.xaml", "localizationKey")]
    [InlineData("", "Themes/Custom.xaml", "localizationKey")]
    [InlineData(" ", "Themes/Custom.xaml", "localizationKey")]
    [InlineData(" Theme_Custom", "Themes/Custom.xaml", "localizationKey")]
    [InlineData("Theme_Custom ", "Themes/Custom.xaml", "localizationKey")]
    [InlineData("Theme_VS2022Light", "Themes/Custom.xaml", "localizationKey")]
    [InlineData("Theme_Custom", null, "resourcePath")]
    [InlineData("Theme_Custom", "", "resourcePath")]
    [InlineData("Theme_Custom", " ", "resourcePath")]
    [InlineData("Theme_Custom", " Themes/Custom.xaml", "resourcePath")]
    [InlineData("Theme_Custom", "Themes/Custom.xaml ", "resourcePath")]
    [InlineData("Theme_Custom", "Themes/Custom.txt", "resourcePath")]
    [InlineData("Theme_Custom", "https://example.org/Custom.xaml", "resourcePath")]
    [InlineData("Theme_Custom", "file:///C:/Custom.xaml", "resourcePath")]
    public void InvalidEntry_DiscardsPartialDataAndReportsIndexAndField(string? key, string? path, string field)
    {
        var entry = JsonSerializer.Serialize(new { localizationKey = key, resourcePath = path });
        var logger = new ResourceCatalogTestLogger();
        AssertFallback(ThemeCatalog.CreateLazy(() => JsonStream($"[{DefaultEntry},{entry}]"), logger).Value, logger);
        Assert.Contains($"Entry[1].{field}", Assert.Single(logger.Entries).Message);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    [InlineData("uk-UA")]
    public void KeysAreOrdinalAndCatalogIsIndependentOfCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUI = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var json = "[" + DefaultEntry + "," +
                """{"localizationKey":"theme_vs2022light","resourcePath":"Themes/Custom.xaml"}]""";
            var catalog = ThemeCatalog.CreateLazy(() => JsonStream(json)).Value;
            Assert.False(catalog.IsFallback);
            Assert.Equal(["Theme_VS2022Light", "theme_vs2022light"], catalog.Themes.Select(theme => theme.LocalizationKey));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUI;
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("open")]
    [InlineData("read")]
    [InlineData("access")]
    [InlineData("manifest")]
    public void LoadFailure_IsCachedAndWarnsOnceUnderConcurrentAccess(string failure)
    {
        var logger = new ResourceCatalogTestLogger();
        var reads = 0;
        var lazy = ThemeCatalog.CreateLazy(() =>
        {
            Interlocked.Increment(ref reads);
            return OpenFailure(failure);
        }, logger);
        var snapshots = new ThemeCatalog[16];
        Parallel.For(0, snapshots.Length, index => snapshots[index] = lazy.Value);
        Assert.All(snapshots, snapshot => Assert.Same(lazy.Value, snapshot));
        Assert.Equal(1, reads);
        AssertFallback(lazy.Value, logger);
    }

    [Theory]
    [InlineData(ValidJson, false)]
    [InlineData("{", true)]
    public void SourceStream_IsDisposed(string json, bool isFallback)
    {
        using var stream = JsonStream(json);
        Assert.Equal(isFallback, ThemeCatalog.CreateLazy(() => stream).Value.IsFallback);
        Assert.False(stream.CanRead);
    }

    [Fact]
    public void ProgrammingFailure_IsNotConvertedToFallback()
    {
        var logger = new ResourceCatalogTestLogger();
        var lazy = ThemeCatalog.CreateLazy(() => throw new InvalidOperationException("Bug."), logger);
        Assert.Throws<InvalidOperationException>(() => lazy.Value);
        Assert.Empty(logger.Entries);
    }

    private static void AssertFallback(ThemeCatalog catalog, ResourceCatalogTestLogger logger)
    {
        Assert.True(catalog.IsFallback);
        Assert.Equal(new ThemeDefinition("Theme_VS2022Light", "Themes/VS2022LightTheme.xaml"), Assert.Single(catalog.Themes));
        Assert.Throws<NotSupportedException>(() => ((IList<ThemeDefinition>)catalog.Themes).Clear());
        logger.AssertCatalogWarning(ThemeCatalog.ResourceName);
    }
}

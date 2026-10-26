using System.Globalization;
using System.Resources;
using System.Reflection;
using KID.Services.Localization;

namespace KID.Tests.Localization;

public sealed class LanguageCatalogResourceTests
{
    [Fact]
    public void EmbeddedCatalogLoadsAllLanguagesAndOwnDisplayNamesWithoutFallback()
    {
        var assembly = typeof(LocalizationService).Assembly;
        Assert.Contains(LanguageCatalog.ResourceName, assembly.GetManifestResourceNames());
        Assert.DoesNotContain("KID.Resources.AvailableLanguage.resources", assembly.GetManifestResourceNames());
        var manager = new ResourceManager("KID.Resources.Strings", assembly);
        var logger = new CatalogTestLogger();

        try
        {
            var catalog = LanguageCatalog.CreateLazy(
                () => assembly.GetManifestResourceStream(LanguageCatalog.ResourceName),
                culture => manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false),
                logger).Value;

            Assert.False(catalog.IsFallback);
            Assert.Empty(logger.Entries);
            Assert.Equal(["en", "ru", "uk"],
                catalog.Languages.Select(language => language.CultureCode));
            Assert.Equal(["Language_English", "Language_Russian", "Language_Ukrainian"], catalog.LanguageKeys);

            foreach (var language in catalog.Languages)
            {
                var resourceSet = manager.GetResourceSet(CultureInfo.GetCultureInfo(language.CultureCode),
                    createIfNotExists: true, tryParents: false);
                Assert.NotNull(resourceSet);
                Assert.Equal(language.CultureCode, resourceSet.GetString("Metadata_CultureCode", ignoreCase: false));
                foreach (var key in catalog.LanguageKeys)
                    Assert.False(string.IsNullOrWhiteSpace(resourceSet.GetString(key, ignoreCase: false)),
                        $"Missing '{key}' in '{language.CultureCode}'.");
            }
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    [Fact]
    public void EnglishEmergencyEntryHasOwnShippedResources()
    {
        var manager = new ResourceManager("KID.Resources.Strings", typeof(LocalizationService).Assembly);
        try
        {
            var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en"),
                createIfNotExists: true, tryParents: false);

            Assert.NotNull(english);
            Assert.Equal("en", english.GetString("Metadata_CultureCode", ignoreCase: false));
            Assert.False(string.IsNullOrWhiteSpace(english.GetString("Language_English", ignoreCase: false)));
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    [Fact]
    public void EnglishFallbackResourcesAreInMainAssembly()
    {
        var assembly = typeof(LocalizationService).Assembly;
        var attribute = assembly.GetCustomAttribute<NeutralResourcesLanguageAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("en", attribute.CultureName);
        Assert.Equal(UltimateResourceFallbackLocation.MainAssembly, attribute.Location);
        Assert.Contains("KID.Resources.Strings.resources", assembly.GetManifestResourceNames());
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("ru-RU", "ru")]
    [InlineData("ru-BY", "ru")]
    [InlineData("uk-UA", "uk")]
    [InlineData("de-DE", "en")]
    public void ResourceLookup_UsesLanguageParentsAndEnglishFallback(string requested, string expected)
    {
        var manager = new ResourceManager("KID.Resources.Strings", typeof(LocalizationService).Assembly);
        try
        {
            Assert.Equal(expected, manager.GetString("Metadata_CultureCode", CultureInfo.GetCultureInfo(requested)));
            Assert.Equal(manager.GetString("Menu_File", CultureInfo.GetCultureInfo(expected)),
                manager.GetString("Menu_File", CultureInfo.GetCultureInfo(requested)));
        }
        finally { manager.ReleaseAllResources(); }
    }
}

using System.Globalization;
using System.Resources;
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
            Assert.Equal(["en-US", "ru-RU", "uk-UA"],
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
            var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en-US"),
                createIfNotExists: true, tryParents: false);

            Assert.NotNull(english);
            Assert.Equal("en-US", english.GetString("Metadata_CultureCode", ignoreCase: false));
            Assert.False(string.IsNullOrWhiteSpace(english.GetString("Language_English", ignoreCase: false)));
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }
}

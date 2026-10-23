using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;
using KID.Services.Diagnostics;
using KID.Services.Localization;
using Microsoft.Extensions.Logging;

namespace KID.Tests.Localization;

public sealed class LanguageCatalogTests
{
    private const string ValidJson = """
        [
          {"cultureCode":"en","displayNameKey":"Language_English"},
          {"cultureCode":"ru","displayNameKey":"Language_Russian"}
        ]
        """;

    [Fact]
    public void CreateLazy_ValidCatalogPublishesOneConsistentImmutableSnapshot()
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var reads = 0;
        var lazy = LanguageCatalog.CreateLazy(() =>
        {
            Interlocked.Increment(ref reads);
            return JsonStream(ValidJson);
        }, resources.Get, logger);

        Assert.Equal(0, reads);
        var snapshots = new LanguageCatalog[16];
        Parallel.For(0, snapshots.Length, index => snapshots[index] = lazy.Value);
        var catalog = lazy.Value;

        Assert.All(snapshots, snapshot => Assert.Same(catalog, snapshot));
        Assert.Equal(1, reads);
        Assert.Empty(logger.Entries);
        Assert.False(catalog.IsFallback);
        Assert.Equal(["en", "ru"], catalog.Languages.Select(language => language.CultureCode));
        Assert.Equal(["Language_English", "Language_Russian"], catalog.LanguageKeys);
        Assert.Equal("ru", catalog.KeyToCulture["language_russian"]);
        Assert.Equal("Language_Russian", catalog.CultureToKey["RU"]);
        AssertConsistent(catalog);

        var keys = (IList<string>)catalog.LanguageKeys;
        Assert.Throws<NotSupportedException>(() => keys[0] = "corrupted");
        Assert.Throws<NotSupportedException>(() => ((IList<LanguageDefinition>)catalog.Languages).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)catalog.KeyToCulture)["Language_English"] = "ru");
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)catalog.CultureToKey).Clear());
        AssertConsistent(lazy.Value);

        // The loader must leave shared ResourceManager-owned sets usable.
        Assert.Equal("English", resources.Get(CultureInfo.GetCultureInfo("en"))!
            .GetString("Language_English"));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[1]")]
    [InlineData("[{}]")]
    [InlineData("[{\"cultureCode\":\"en\"}]")]
    [InlineData("[{\"displayNameKey\":\"Language_English\"}]")]
    [InlineData("[{\"cultureCode\":null,\"displayNameKey\":\"Language_English\"}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"displayNameKey\":null}]")]
    [InlineData("[{\"cultureCode\":7,\"displayNameKey\":\"Language_English\"}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"displayNameKey\":false}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"displayNameKeey\":\"Language_English\"}]")]
    [InlineData("[{\"CultureCode\":\"en\",\"displayNameKey\":\"Language_English\"}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"displayNameKey\":\"Language_English\",\"extra\":1}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"cultureCode\":\"ru\",\"displayNameKey\":\"Language_English\"}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"displayNameKey\":\"Language_English\",}]")]
    [InlineData("[/* comment */{\"cultureCode\":\"en\",\"displayNameKey\":\"Language_English\"}]")]
    [InlineData("[{\"cultureCode\":\"en\",\"displayNameKey\":\"Language_English\"},]")]
    public void CreateLazy_InvalidJsonRejectsWholeCatalog(string json)
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var catalog = LanguageCatalog.CreateLazy(() => JsonStream(json), resources.Get, logger).Value;

        AssertFallback(catalog, logger);
    }

    [Theory]
    [InlineData("", "Language_English", "cultureCode")]
    [InlineData(" ", "Language_English", "cultureCode")]
    [InlineData(" en", "Language_English", "cultureCode")]
    [InlineData("en ", "Language_English", "cultureCode")]
    [InlineData("invalid/culture", "Language_English", "cultureCode")]
    [InlineData("en", "", "displayNameKey")]
    [InlineData("en", " ", "displayNameKey")]
    [InlineData("en", " Language_English", "displayNameKey")]
    [InlineData("en", "Language_English ", "displayNameKey")]
    public void CreateLazy_InvalidValueReportsIndexFieldAndValue(string culture, string key, string field)
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var json = $$"""[{"cultureCode":"{{culture}}","displayNameKey":"{{key}}"}]""";

        AssertFallback(LanguageCatalog.CreateLazy(() => JsonStream(json), resources.Get, logger).Value, logger);
        var message = Assert.Single(logger.Entries).Message;
        Assert.Contains($"Entry[0].{field}", message);
        Assert.Contains($"'{(field == "cultureCode" ? culture : key)}'", message);
    }

    [Theory]
    [InlineData("en", "Other_Key", "cultureCode")]
    [InlineData("EN", "Other_Key", "cultureCode")]
    [InlineData("ru", "Language_English", "displayNameKey")]
    [InlineData("ru", "language_english", "displayNameKey")]
    public void CreateLazy_DuplicateAfterValidEntryDiscardsPartialResult(string culture, string key, string field)
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var json = $$"""
            [
              {"cultureCode":"en","displayNameKey":"Language_English"},
              {"cultureCode":"{{culture}}","displayNameKey":"{{key}}"}
            ]
            """;

        AssertFallback(LanguageCatalog.CreateLazy(() => JsonStream(json), resources.Get, logger).Value, logger);
        var message = Assert.Single(logger.Entries).Message;
        Assert.Contains($"Entry[1].{field}", message);
        Assert.Contains($"'{(field == "cultureCode" ? culture : key)}'", message);
    }

    [Fact]
    public void CreateLazy_NormalizesCultureBeforeStoringIt()
    {
        using var resources = new CatalogTestResources();
        var json = """[{"cultureCode":"EN","displayNameKey":"Language_English"}]""";
        var catalog = LanguageCatalog.CreateLazy(() => JsonStream(json), resources.Get).Value;

        Assert.False(catalog.IsFallback);
        Assert.Equal("en", Assert.Single(catalog.Languages).CultureCode);
        Assert.Equal("en", catalog.KeyToCulture["Language_English"]);
    }

    [Fact]
    public void CreateLazy_MissingOwnResourceSetRejectsCatalog()
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var lazy = LanguageCatalog.CreateLazy(() => JsonStream(ValidJson),
            culture => culture.Name == "ru" ? null : resources.Get(culture), logger);

        AssertFallback(lazy.Value, logger);
        Assert.Contains("Entry[1].cultureCode='ru'", Assert.Single(logger.Entries).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("ru-RU")]
    [InlineData(" ru ")]
    public void CreateLazy_MissingOrMismatchedMetadataRejectsCatalog(string? metadata)
    {
        using var resources = new CatalogTestResources();
        resources.Set("ru", "Metadata_CultureCode", metadata);
        var logger = new CatalogTestLogger();

        AssertFallback(LanguageCatalog.CreateLazy(() => JsonStream(ValidJson), resources.Get, logger).Value, logger);
        Assert.Contains("Metadata_CultureCode", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public void CreateLazy_CachedFallbackResourceSetCannotConfirmAnotherCulture()
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var english = resources.Get(CultureInfo.GetCultureInfo("en"));
        var lazy = LanguageCatalog.CreateLazy(() => JsonStream(ValidJson), _ => english, logger);

        AssertFallback(lazy.Value, logger);
        Assert.Contains("Entry[1].cultureCode='ru'", Assert.Single(logger.Entries).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData(42)]
    public void CreateLazy_EnglishFallbackDoesNotSupplyMissingOwnDisplayName(object? value)
    {
        using var resources = new CatalogTestResources();
        resources.Set("ru", "Language_English", value);
        var logger = new CatalogTestLogger();

        AssertFallback(LanguageCatalog.CreateLazy(() => JsonStream(ValidJson), resources.Get, logger).Value, logger);
        Assert.Contains("Entry[0].displayNameKey='Language_English'", Assert.Single(logger.Entries).Message);
        Assert.Contains("'ru'", Assert.Single(logger.Entries).Message);
        Assert.Equal("English", resources.Get(CultureInfo.GetCultureInfo("en"))!
            .GetString("Language_English"));
    }

    [Fact]
    public void CreateLazy_DisplayNameKeyRequiresExactResourceCase()
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var json = """[{"cultureCode":"en","displayNameKey":"language_english"}]""";

        AssertFallback(LanguageCatalog.CreateLazy(() => JsonStream(json), resources.Get, logger).Value, logger);
        Assert.Contains("language_english", Assert.Single(logger.Entries).Message);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("open")]
    [InlineData("read")]
    [InlineData("access")]
    [InlineData("manifest")]
    [InlineData("satellite")]
    public void CreateLazy_LoadFailureReadsAndWarnsOnce(string failure)
    {
        using var resources = new CatalogTestResources();
        var logger = new CatalogTestLogger();
        var reads = 0;
        var lazy = LanguageCatalog.CreateLazy(() =>
        {
            reads++;
            return failure switch
            {
                "missing" => null,
                "open" => throw new IOException("Cannot open stream."),
                "read" => new ThrowingReadStream(),
                "access" => throw new UnauthorizedAccessException("Access denied."),
                "manifest" => throw new MissingManifestResourceException("Missing Strings."),
                "satellite" => throw new MissingSatelliteAssemblyException("Missing satellite."),
                _ => throw new InvalidOperationException()
            };
        }, resources.Get, logger);

        var catalog = lazy.Value;
        AssertFallback(catalog, logger);
        Assert.Same(catalog, lazy.Value);
        Assert.Equal(1, reads);
        Assert.Single(logger.Entries);
        if (failure is "open" or "read")
            Assert.IsType<IOException>(Assert.Single(logger.Entries).Exception);
    }

    [Theory]
    [InlineData(ValidJson, false)]
    [InlineData("{", true)]
    public void CreateLazy_DisposesSourceStream(string json, bool isFallback)
    {
        using var resources = new CatalogTestResources();
        using var stream = JsonStream(json);

        Assert.Equal(isFallback, LanguageCatalog.CreateLazy(() => stream, resources.Get).Value.IsFallback);
        Assert.False(stream.CanRead);
    }

    [Fact]
    public void CreateLazy_FatalFailureIsNotConvertedToFallback()
    {
        var logger = new CatalogTestLogger();
        var lazy = LanguageCatalog.CreateLazy(() => throw new OutOfMemoryException(), _ => null, logger);

        Assert.Throws<OutOfMemoryException>(() => lazy.Value);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN-us", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("en-Latn-US", "en")]
    [InlineData("ru-RU", "ru")]
    [InlineData("ru-BY", "ru")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("de-DE", null)]
    [InlineData("not/a/culture", null)]
    public void ResolveCultureCode_UsesCatalogAndParentChain(string? input, string? expected)
    {
        using var resources = new CatalogTestResources();
        var catalog = LanguageCatalog.CreateLazy(() => JsonStream(ValidJson), resources.Get).Value;

        Assert.Equal(expected, catalog.ResolveCultureCode(input));
    }

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));

    private static void AssertFallback(LanguageCatalog catalog, CatalogTestLogger logger)
    {
        Assert.True(catalog.IsFallback);
        Assert.Equal(new LanguageDefinition("en", "Language_English"), Assert.Single(catalog.Languages));
        Assert.Equal("Language_English", Assert.Single(catalog.LanguageKeys));
        AssertConsistent(catalog);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Equal(DiagnosticEventIds.LocalizationFallback, warning.EventId);
        Assert.NotNull(warning.Exception);
        Assert.Contains(LanguageCatalog.ResourceName, warning.Message);
        Assert.Contains(warning.Exception.Message, warning.Message);
    }

    private static void AssertConsistent(LanguageCatalog catalog)
    {
        Assert.Equal(catalog.Languages.Length, catalog.KeyToCulture.Count);
        Assert.Equal(catalog.Languages.Length, catalog.CultureToKey.Count);
        Assert.Equal(catalog.Languages.Select(language => language.DisplayNameKey), catalog.LanguageKeys);
        foreach (var language in catalog.Languages)
        {
            Assert.Equal(language.CultureCode, catalog.KeyToCulture[language.DisplayNameKey]);
            Assert.Equal(language.DisplayNameKey, catalog.CultureToKey[language.CultureCode]);
        }
        Assert.False(catalog.KeyToCulture.ContainsKey("unknown"));
        Assert.False(catalog.CultureToKey.ContainsKey("unknown"));
    }

    private sealed class ThrowingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Read failed.");
        public override int Read(Span<byte> buffer) => throw new IOException("Read failed.");
    }
}

internal sealed class CatalogTestResources : IDisposable
{
    private readonly Dictionary<string, Dictionary<string, object>> values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResourceSet> resourceSets = new(StringComparer.OrdinalIgnoreCase);

    internal CatalogTestResources()
    {
        foreach (var culture in new[] { "en", "ru", "uk" })
        {
            values.Add(culture, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Metadata_CultureCode"] = culture,
                ["Language_English"] = "English",
                ["Language_Russian"] = "Russian",
                ["Language_Ukrainian"] = "Ukrainian"
            });
        }
    }

    internal void Set(string culture, string key, object? value)
    {
        if (resourceSets.Remove(culture, out var cached))
            cached.Dispose();
        if (value is null)
            values[culture].Remove(key);
        else
            values[culture][key] = value;
    }

    internal ResourceSet? Get(CultureInfo culture)
    {
        if (resourceSets.TryGetValue(culture.Name, out var cached))
            return cached;
        if (!values.TryGetValue(culture.Name, out var strings))
            return null;

        using var output = new MemoryStream();
        using (var writer = new ResourceWriter(output))
        {
            foreach (var (key, value) in strings)
                writer.AddResource(key, value);
            writer.Generate();
        }

        var resourceSet = new ResourceSet(new MemoryStream(output.ToArray()));
        resourceSets.Add(culture.Name, resourceSet);
        return resourceSet;
    }

    public void Dispose()
    {
        foreach (var resourceSet in resourceSets.Values)
            resourceSet.Dispose();
    }
}

internal sealed class CatalogTestLogger : ILogger
{
    internal List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));

    internal sealed record LogEntry(LogLevel Level, EventId EventId, Exception? Exception, string Message);
}

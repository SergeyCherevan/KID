using System.Globalization;
using KID.Services.Fonts;
using KID.Tests.Infrastructure;
using static KID.Tests.Infrastructure.ResourceCatalogTestSupport;

namespace KID.Tests.Fonts;

public sealed class FontCatalogTests
{
    internal const string ValidJson = """{"fonts":["Fira Code","Consolas"],"fontSizes":[14,10.5,22]}""";

    [Fact]
    public void ValidCatalog_IsLoadedOnceAndPublishesImmutableOrderedSnapshot()
    {
        var logger = new ResourceCatalogTestLogger();
        var reads = 0;
        var lazy = FontCatalog.CreateLazy(() =>
        {
            Interlocked.Increment(ref reads);
            return JsonStream(ValidJson);
        }, logger);
        Assert.Equal(0, reads);
        var snapshots = new FontCatalog[16];
        Parallel.For(0, snapshots.Length, index => snapshots[index] = lazy.Value);
        var catalog = lazy.Value;

        Assert.All(snapshots, snapshot => Assert.Same(catalog, snapshot));
        Assert.Equal(1, reads);
        Assert.Empty(logger.Entries);
        Assert.False(catalog.IsFallback);
        Assert.Equal(["Fira Code", "Consolas"], catalog.Fonts);
        Assert.Equal([14, 10.5, 22], catalog.FontSizes);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)catalog.Fonts)[0] = "corrupted");
        Assert.Throws<NotSupportedException>(() => ((IList<double>)catalog.FontSizes).Clear());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"fonts\":[\"Consolas\"]}")]
    [InlineData("{\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":null,\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[],\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":null}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[]}")]
    [InlineData("{\"fonts\":\"Consolas\",\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[42],\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[\"14\"]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[null]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[true]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[14],\"extra\":true}")]
    [InlineData("{\"Fonts\":[\"Consolas\"],\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fonts\":[\"Fira Code\"],\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[14],\"fontSizes\":[16]}")]
    [InlineData("{/* comment */\"fonts\":[\"Consolas\"],\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[\"Consolas\",],\"fontSizes\":[14]}")]
    [InlineData("{\"fonts\":[\"Consolas\"],\"fontSizes\":[14],}")]
    public void InvalidJson_RejectsWholeCatalog(string json)
    {
        var logger = new ResourceCatalogTestLogger();
        AssertFallback(FontCatalog.CreateLazy(() => JsonStream(json), logger).Value, logger);
    }

    [Theory]
    [InlineData("{\"fonts\":[\"Fira Code\",null],\"fontSizes\":[22]}", "fonts[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\",\"\"],\"fontSizes\":[22]}", "fonts[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\",\" \"],\"fontSizes\":[22]}", "fonts[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\",\" Consolas\"],\"fontSizes\":[22]}", "fonts[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\",\"Consolas \"],\"fontSizes\":[22]}", "fonts[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\",\"fira code\"],\"fontSizes\":[22]}", "fonts[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\"],\"fontSizes\":[22,0]}", "fontSizes[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\"],\"fontSizes\":[22,-1]}", "fontSizes[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\"],\"fontSizes\":[22,22.0]}", "fontSizes[1]")]
    [InlineData("{\"fonts\":[\"Fira Code\"],\"fontSizes\":[22,1e999]}", "fontSizes[1]")]
    public void InvalidEntry_DiscardsPartialDataAndReportsField(string json, string field)
    {
        var logger = new ResourceCatalogTestLogger();
        AssertFallback(FontCatalog.CreateLazy(() => JsonStream(json), logger).Value, logger);
        Assert.Contains(field, Assert.Single(logger.Entries).Message);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    [InlineData("uk-UA")]
    public void NumbersAndOrder_AreIndependentOfCurrentCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUI = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var catalog = FontCatalog.CreateLazy(() => JsonStream(ValidJson)).Value;
            Assert.False(catalog.IsFallback);
            Assert.Equal([14, 10.5, 22], catalog.FontSizes);
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
        var lazy = FontCatalog.CreateLazy(() =>
        {
            Interlocked.Increment(ref reads);
            return OpenFailure(failure);
        }, logger);
        var snapshots = new FontCatalog[16];
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
        Assert.Equal(isFallback, FontCatalog.CreateLazy(() => stream).Value.IsFallback);
        Assert.False(stream.CanRead);
    }

    [Fact]
    public void ProgrammingFailure_IsNotConvertedToFallback()
    {
        var logger = new ResourceCatalogTestLogger();
        var lazy = FontCatalog.CreateLazy(() => throw new InvalidOperationException("Bug."), logger);
        Assert.Throws<InvalidOperationException>(() => lazy.Value);
        Assert.Empty(logger.Entries);
    }

    private static void AssertFallback(FontCatalog catalog, ResourceCatalogTestLogger logger)
    {
        Assert.True(catalog.IsFallback);
        Assert.Equal(["Consolas"], catalog.Fonts);
        Assert.Equal([10, 11, 12, 13, 14, 16, 18, 20], catalog.FontSizes);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)catalog.Fonts).Clear());
        logger.AssertCatalogWarning(FontCatalog.ResourceName);
    }
}

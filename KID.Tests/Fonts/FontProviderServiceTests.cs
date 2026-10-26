using System.IO;
using KID.Services.Diagnostics;
using KID.Services.Fonts;
using KID.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using static KID.Tests.Infrastructure.ResourceCatalogTestSupport;

namespace KID.Tests.Fonts;

public sealed class FontProviderServiceTests
{
    [Fact]
    public void AvailableFonts_FiltersCaseInsensitivelyInCatalogOrderAndRefreshesInstalledNames()
    {
        var reads = 0;
        string[] installed = ["consolas", "FIRA CODE", "consolas", "Arial"];
        var provider = new FontProviderService(() =>
        {
            reads++;
            return JsonStream(FontCatalogTests.ValidJson);
        }, () => installed);

        Assert.Equal(["Fira Code", "Consolas"], provider.GetAvailableFonts());
        installed = ["CONSOLAS"];
        Assert.Equal(["Consolas"], provider.GetAvailableFonts());
        Assert.Equal([14, 10.5, 22], provider.GetAvailableFontSizes());
        Assert.Equal(1, reads);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)provider.GetAvailableFontSizes()).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)provider.GetAvailableFonts()).Clear());
    }

    [Fact]
    public void EmptyIntersection_ReturnsConsolasWithoutWarning()
    {
        var logger = new ResourceCatalogTestLogger();
        var provider = new FontProviderService(() => JsonStream(FontCatalogTests.ValidJson), () => ["Arial"], logger);
        Assert.Equal(["Consolas"], provider.GetAvailableFonts());
        Assert.Equal([14, 10.5, 22], provider.GetAvailableFontSizes());
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void SystemFontFailure_DoesNotReplaceValidSizesAndCanRecoverOnNextRequest()
    {
        var logger = new ResourceCatalogTestLogger();
        var fail = true;
        var provider = new FontProviderService(() => JsonStream(FontCatalogTests.ValidJson),
            () => fail ? ThrowDuringEnumeration() : ["Fira Code"], logger);

        Assert.Equal(["Consolas"], provider.GetAvailableFonts());
        Assert.Equal([14, 10.5, 22], provider.GetAvailableFontSizes());
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Equal(DiagnosticEventIds.SettingsFallback, warning.EventId);
        Assert.IsType<IOException>(warning.Exception);
        fail = false;
        Assert.Equal(["Fira Code"], provider.GetAvailableFonts());
    }

    [Fact]
    public void InvalidCatalog_ReturnsWholeCachedFallbackWithoutEnumeratingWindowsFonts()
    {
        var logger = new ResourceCatalogTestLogger();
        var provider = new FontProviderService(() => JsonStream("{}"),
            () => throw new InvalidOperationException("Must not enumerate Windows fonts."), logger);
        Assert.Equal([10, 11, 12, 13, 14, 16, 18, 20], provider.GetAvailableFontSizes());
        Assert.Equal(["Consolas"], provider.GetAvailableFonts());
        Assert.Equal(["Consolas"], provider.GetAvailableFonts());
        logger.AssertCatalogWarning(FontCatalog.ResourceName);
    }

    private static IEnumerable<string> ThrowDuringEnumeration()
    {
        yield return "Fira Code";
        throw new IOException("Installed font enumeration failed.");
    }
}

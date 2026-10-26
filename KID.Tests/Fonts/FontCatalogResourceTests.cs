using KID.Services.Fonts;
using KID.Tests.Infrastructure;

namespace KID.Tests.Fonts;

public sealed class FontCatalogResourceTests
{
    [Fact]
    public void ShippedCatalog_HasExpectedContentsAndLoadsWithoutFallback()
    {
        var assembly = typeof(FontProviderService).Assembly;
        Assert.Contains(FontCatalog.ResourceName, assembly.GetManifestResourceNames());
        Assert.DoesNotContain("KID.Resources.AvailableFontsAndSizes.resources", assembly.GetManifestResourceNames());
        var logger = new ResourceCatalogTestLogger();
        var catalog = FontCatalog.CreateLazy(() => assembly.GetManifestResourceStream(FontCatalog.ResourceName), logger).Value;

        Assert.False(catalog.IsFallback);
        Assert.Empty(logger.Entries);
        Assert.Equal(["Consolas", "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Fira Code",
            "Source Code Pro", "Courier New", "Lucida Console", "Segoe UI Mono"], catalog.Fonts);
        Assert.Equal([10, 11, 12, 13, 14, 16, 18, 20], catalog.FontSizes);
    }
}

using System.Windows;
using KID.Services.Themes;
using KID.Tests.Infrastructure;

namespace KID.Tests.Themes;

public sealed class ThemeResourceIsolationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReplaceTheme_PreservesOtherDictionariesAndLookupPriority(int containerIndex)
    {
        await StaTest.RunAsync(() =>
        {
            var rootValue = new object();
            var beforeValue = new object();
            var afterValue = new object();
            var resources = new ResourceDictionary { ["RootOverride"] = rootValue };
            var before = new ResourceDictionary
            {
                ["BeforeOnly"] = beforeValue,
                ["PriorityKey"] = beforeValue
            };
            var after = new ResourceDictionary
            {
                ["AfterOnly"] = afterValue,
                ["PriorityKey"] = afterValue
            };
            var container = new ThemeResourceContainer();
            container.MergedDictionaries.Add(new ResourceDictionary { ["OldThemeOnly"] = new object() });
            resources.MergedDictionaries.Add(before);
            resources.MergedDictionaries.Add(after);
            resources.MergedDictionaries.Insert(containerIndex, container);
            var outerDictionaries = resources.MergedDictionaries.ToArray();
            var host = new FrameworkElement { Resources = resources };

            for (var iteration = 0; iteration < 5; iteration++)
            {
                var themeValue = new object();
                var theme = new ResourceDictionary
                {
                    ["ThemeOnly"] = themeValue,
                    ["PriorityKey"] = themeValue,
                    ["RootOverride"] = themeValue
                };

                ThemeService.ReplaceThemeDictionary(resources, theme);

                Assert.Equal(outerDictionaries.Length, resources.MergedDictionaries.Count);
                for (var index = 0; index < outerDictionaries.Length; index++)
                    Assert.Same(outerDictionaries[index], resources.MergedDictionaries[index]);

                Assert.Same(theme, Assert.Single(container.MergedDictionaries));
                Assert.Null(container.Source);
                Assert.Empty(container.Keys);
                Assert.Equal(2, before.Count);
                Assert.Equal(2, after.Count);
                Assert.Empty(before.MergedDictionaries);
                Assert.Empty(after.MergedDictionaries);
                Assert.Same(beforeValue, before["PriorityKey"]);
                Assert.Same(afterValue, after["PriorityKey"]);
                Assert.Same(beforeValue, host.FindResource("BeforeOnly"));
                Assert.Same(afterValue, host.FindResource("AfterOnly"));
                Assert.Same(rootValue, host.FindResource("RootOverride"));
                Assert.Same(themeValue, host.FindResource("ThemeOnly"));
                Assert.Same(containerIndex == 2 ? themeValue : afterValue, host.FindResource("PriorityKey"));
                Assert.False(resources.Contains("OldThemeOnly"));
            }
        });
    }

    [Theory]
    [InlineData("Missing", "exactly one theme resource container")]
    [InlineData("Nested", "exactly one theme resource container")]
    [InlineData("Duplicate", "exactly one theme resource container")]
    [InlineData("Empty", "exactly one active theme dictionary")]
    [InlineData("MultipleThemes", "exactly one active theme dictionary")]
    [InlineData("DirectResource", "must not have a source or direct resources")]
    [InlineData("Source", "must not have a source or direct resources")]
    public async Task InvalidStructure_RejectsReplacementWithoutChangingResources(string scenario, string expectedMessage)
    {
        await StaTest.RunAsync(() =>
        {
            var resources = new ResourceDictionary { ["RootResource"] = new object() };
            var before = new ResourceDictionary { ["BeforeResource"] = new object() };
            var after = new ResourceDictionary { ["AfterResource"] = new object() };
            var container = new ThemeResourceContainer();
            container.MergedDictionaries.Add(new ResourceDictionary { ["ActiveThemeResource"] = new object() });
            resources.MergedDictionaries.Add(before);
            resources.MergedDictionaries.Add(container);
            resources.MergedDictionaries.Add(after);

            switch (scenario)
            {
                case "Missing":
                    resources.MergedDictionaries.Remove(container);
                    break;
                case "Nested":
                    resources.MergedDictionaries.Remove(container);
                    before.MergedDictionaries.Add(container);
                    break;
                case "Duplicate":
                    var duplicate = new ThemeResourceContainer();
                    duplicate.MergedDictionaries.Add(new ResourceDictionary());
                    resources.MergedDictionaries.Add(duplicate);
                    break;
                case "Empty":
                    container.MergedDictionaries.Clear();
                    break;
                case "MultipleThemes":
                    container.MergedDictionaries.Add(new ResourceDictionary { ["AnotherThemeResource"] = new object() });
                    break;
                case "DirectResource":
                    container["DirectResource"] = new object();
                    break;
                case "Source":
                    container.Source = new Uri("/KID.WPF.IDE;component/Themes/VS2022Metrics.xaml", UriKind.Relative);
                    // Изолируем ошибку Source: другие инварианты остаются выполненными.
                    container.Clear();
                    container.MergedDictionaries.Clear();
                    container.MergedDictionaries.Add(new ResourceDictionary { ["ActiveThemeResource"] = new object() });
                    Assert.NotNull(container.Source);
                    Assert.Empty(container.Keys);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown test scenario: {scenario}");
            }

            var originalGraph = CaptureGraph(resources).ToArray();
            var replacement = new ResourceDictionary { ["ReplacementResource"] = new object() };

            var exception = Assert.Throws<InvalidOperationException>(
                () => ThemeService.ReplaceThemeDictionary(resources, replacement));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
            foreach (var snapshot in originalGraph)
            {
                Assert.Equal(snapshot.Source, snapshot.Dictionary.Source);
                Assert.Equal(snapshot.Entries.Length, snapshot.Dictionary.Count);
                foreach (var entry in snapshot.Entries)
                    Assert.Same(entry.Value, snapshot.Dictionary[entry.Key]);

                Assert.Equal(snapshot.MergedDictionaries.Length, snapshot.Dictionary.MergedDictionaries.Count);
                for (var index = 0; index < snapshot.MergedDictionaries.Length; index++)
                    Assert.Same(snapshot.MergedDictionaries[index], snapshot.Dictionary.MergedDictionaries[index]);
            }
            Assert.False(resources.Contains("ReplacementResource"));
        });
    }

    private static IEnumerable<ResourceSnapshot> CaptureGraph(ResourceDictionary dictionary)
    {
        var entries = dictionary.Keys.Cast<object>()
            .Select(key => new KeyValuePair<object, object?>(key, dictionary[key]))
            .ToArray();
        var mergedDictionaries = dictionary.MergedDictionaries.ToArray();
        yield return new ResourceSnapshot(dictionary, dictionary.Source, entries, mergedDictionaries);
        foreach (var mergedDictionary in mergedDictionaries)
        {
            foreach (var snapshot in CaptureGraph(mergedDictionary))
                yield return snapshot;
        }
    }

    private sealed record ResourceSnapshot(
        ResourceDictionary Dictionary,
        Uri? Source,
        KeyValuePair<object, object?>[] Entries,
        ResourceDictionary[] MergedDictionaries);
}

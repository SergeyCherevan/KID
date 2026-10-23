using System.Globalization;
using KID.Models;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace KID.Tests.Localization;

[Collection(LocalizationServiceCollection.Name)]
public sealed class LocalizationServiceTests
{
    [Fact]
    public void LanguageCatalog_PreservesPublicOrderMappingsAndImmutableState()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var service = new LocalizationService(serviceProvider);
        var keys = service.GetAvailableLanguages();

        Assert.Equal(["Language_English", "Language_Russian", "Language_Ukrainian"], keys);
        Assert.Equal("en", service.GetCultureCodeByLanguageKey("language_english"));
        Assert.Equal("ru", service.GetCultureCodeByLanguageKey("Language_Russian"));
        Assert.Equal("uk", service.GetCultureCodeByLanguageKey("Language_Ukrainian"));
        Assert.Equal("Language_English", service.GetLanguageKeyByCultureCode("EN"));
        Assert.Equal("Language_Russian", service.GetLanguageKeyByCultureCode("ru"));
        Assert.Equal("Language_Ukrainian", service.GetLanguageKeyByCultureCode("uk"));

        var list = Assert.IsAssignableFrom<IList<string>>(keys);
        Assert.Throws<NotSupportedException>(() => list[0] = "corrupted");
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Equal(["Language_English", "Language_Russian", "Language_Ukrainian"], service.GetAvailableLanguages());
        Assert.Equal("en", service.GetCultureCodeByLanguageKey("Language_English"));
        Assert.Equal("Language_English", service.GetLanguageKeyByCultureCode("en"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void LanguageCatalog_UnknownKeyOrCultureReturnsEmptyString(string? value)
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var service = new LocalizationService(serviceProvider);

        Assert.Equal(string.Empty, service.GetCultureCodeByLanguageKey(value!));
        Assert.Equal(string.Empty, service.GetLanguageKeyByCultureCode(value!));
    }

    [Fact]
    public async Task SetCultureAsync_AppliesCultureImmediatelyAndAwaitsSettingsSave()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var configurationService = new BlockingWindowConfigurationService();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IWindowConfigurationService>(configurationService)
            .BuildServiceProvider();
        var service = new LocalizationService(serviceProvider);
        var targetCulture = service.CurrentCulture == "en" ? "ru" : "en";
        var cultureChanged = false;
        service.CultureChanged += (_, _) => cultureChanged = true;

        try
        {
            var changeTask = service.SetCultureAsync(targetCulture);
            await configurationService.SaveStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(targetCulture, service.CurrentCulture);
            Assert.True(cultureChanged);
            Assert.False(changeTask.IsCompleted);
            Assert.Equal(targetCulture, configurationService.SavedCultureCode);

            configurationService.ReleaseSave();
            await changeTask.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            configurationService.ReleaseSave();
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("en-US", "en", "en-US")]
    [InlineData("en-GB", "en", "en-US")]
    [InlineData("ru-RU", "ru", "ru-RU")]
    [InlineData("ru-BY", "ru", "ru-RU")]
    [InlineData("uk-UA", "uk", "uk-UA")]
    public async Task SetCultureAsync_ResolvesAliasesAndKeepsSpecificFormatting(
        string input, string canonical, string formatting)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LocalizationService(provider);
        await service.SetCultureAsync(canonical == "en" ? "ru" : "en");
        string? observedUiCulture = null;
        string? observedFormatting = null;
        service.CultureChanged += (_, _) =>
        {
            observedUiCulture = CultureInfo.CurrentUICulture.Name;
            observedFormatting = CultureInfo.CurrentCulture.Name;
        };

        await service.SetCultureAsync(input);

        Assert.Equal(canonical, service.CurrentCulture);
        Assert.Equal(canonical, observedUiCulture);
        Assert.Equal(formatting, observedFormatting);
        Assert.Equal(service.GetLanguageKeyByCultureCode(canonical), service.GetLanguageKeyByCultureCode(input));
        Assert.DoesNotContain("[", service.GetString("Menu_File"));
        Assert.Contains("12", service.GetString("Error_Compilation", 12, "example"));
        Assert.Equal("[Missing_Test_Key]", service.GetString("Missing_Test_Key"));
        Assert.Equal("[Missing_Test_Key]", service.GetString("Missing_Test_Key", 12));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("not/a/culture")]
    public async Task SetCultureAsync_RejectsUnsupportedInputWithoutChangingState(string input)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LocalizationService(provider);
        var original = service.CurrentCulture;
        var events = 0;
        service.CultureChanged += (_, _) => events++;

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetCultureAsync(input));

        Assert.Equal(original, service.CurrentCulture);
        Assert.Equal(0, events);
        Assert.Equal(string.Empty, service.GetLanguageKeyByCultureCode(input));
    }

    [Fact]
    public async Task SetCultureAsync_UnchangedLanguageStillAwaitsSaveAndRetriesFailure()
    {
        var configuration = new BlockingWindowConfigurationService { FailNextSave = true };
        using var provider = new ServiceCollection()
            .AddSingleton<IWindowConfigurationService>(configuration).BuildServiceProvider();
        var service = new LocalizationService(provider);
        var events = 0;
        service.CultureChanged += (_, _) => events++;
        var alias = CultureInfo.CreateSpecificCulture(service.CurrentCulture).Name;

        configuration.ReleaseSave();
        await Assert.ThrowsAsync<System.IO.IOException>(() => service.SetCultureAsync(alias));
        var retry = service.SetCultureAsync(alias);
        Assert.False(retry.IsCompleted);
        configuration.ReleaseSave();
        await retry;

        Assert.Equal(service.CurrentCulture, configuration.SavedCultureCode);
        Assert.Equal(2, configuration.SaveCalls);
        Assert.Equal(0, events);
    }

    [Theory]
    [InlineData("ru-BY", "ru")]
    [InlineData("de-DE", "en")]
    public void Constructor_ResolvesSystemUiCulture(string systemCulture, string expected)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemCulture);
            using var provider = new ServiceCollection().BuildServiceProvider();
            var service = new LocalizationService(provider);
            Assert.Equal(expected, service.CurrentCulture);
            Assert.DoesNotContain("[", service.GetString("Menu_File"));
        }
        finally { CultureInfo.CurrentUICulture = original; }
    }

    private sealed class BlockingWindowConfigurationService : IWindowConfigurationService
    {
        internal TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly SemaphoreSlim saveBarrier = new(0);

        internal string? SavedCultureCode { get; private set; }
        internal bool FailNextSave { get; set; }
        internal int SaveCalls { get; private set; }

        internal void ReleaseSave() => saveBarrier.Release();

        public WindowConfigurationData Settings { get; } = new();

        public event EventHandler? FontSettingsChanged
        {
            add { }
            remove { }
        }

        public event EventHandler? UILanguageSettingsChanged
        {
            add { }
            remove { }
        }

        public Task SetConfigurationFromFileAsync() => throw new NotSupportedException();

        public Task SetDefaultCodeAsync() => throw new NotSupportedException();

        public Task SaveSettingsAsync() => throw new NotSupportedException();

        public Task SetFontAsync(string? fontFamilyName, double? fontSize) =>
            throw new NotSupportedException();

        public async Task SetUILanguageAsync(string cultureCode)
        {
            SavedCultureCode = cultureCode;
            SaveCalls++;
            SaveStarted.TrySetResult();
            await saveBarrier.WaitAsync();
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new System.IO.IOException("settings write failed");
            }
        }

        public Task SetColorThemeAsync(string themeKey) => throw new NotSupportedException();

        public event EventHandler? IconsVisibilityChanged
        {
            add { }
            remove { }
        }

        public Task SetIconsVisibilityAsync(bool isIconsVisible) => throw new NotSupportedException();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationServiceCollection
{
    public const string Name = "Localization service";
}

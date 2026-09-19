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
    public async Task SetCultureAsync_AppliesCultureImmediatelyAndAwaitsSettingsSave()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var targetCulture = string.Equals(
            originalUiCulture.Name,
            "en-US",
            StringComparison.OrdinalIgnoreCase)
                ? "de-DE"
                : "en-US";
        var configurationService = new BlockingWindowConfigurationService();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IWindowConfigurationService>(configurationService)
            .BuildServiceProvider();
        var service = new LocalizationService(serviceProvider);
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

    private sealed class BlockingWindowConfigurationService : IWindowConfigurationService
    {
        internal TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly SemaphoreSlim saveBarrier = new(0);

        internal string? SavedCultureCode { get; private set; }

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
            SaveStarted.TrySetResult();
            await saveBarrier.WaitAsync();
        }

        public Task SetColorThemeAsync(string themeKey) => throw new NotSupportedException();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationServiceCollection
{
    public const string Name = "Localization service";
}

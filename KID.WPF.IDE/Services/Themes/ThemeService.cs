using System;
using System.Windows;
using KID.Models;
using KID.Services.Errors.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Themes.Interfaces;

namespace KID.Services.Themes;

public sealed class ThemeService : IThemeService
{
    private readonly IThemeProviderService themeProviderService;
    private readonly IWindowConfigurationService windowConfigurationService;
    private readonly IAsyncOperationErrorHandler asyncOperationErrorHandler;
    private readonly App app;

    public ThemeDefinition CurrentTheme { get; private set; }

    public event EventHandler? ThemeChanged;

    public ThemeService(
        IThemeProviderService themeProviderService,
        IWindowConfigurationService windowConfigurationService,
        IAsyncOperationErrorHandler asyncOperationErrorHandler,
        App app)
    {
        this.themeProviderService = themeProviderService ?? throw new ArgumentNullException(nameof(themeProviderService));
        this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
        this.asyncOperationErrorHandler = asyncOperationErrorHandler ?? throw new ArgumentNullException(nameof(asyncOperationErrorHandler));
        this.app = app ?? throw new ArgumentNullException(nameof(app));

        CurrentTheme = themeProviderService.GetDefaultTheme();
    }

    public void ApplyTheme(string? localizationKey)
    {
        var requestedTheme = themeProviderService.TryGetTheme(localizationKey, out var resolvedTheme)
            ? resolvedTheme
            : themeProviderService.GetDefaultTheme();

        if (TryApplyTheme(requestedTheme, out var requestedError))
            return;

        var defaultTheme = themeProviderService.GetDefaultTheme();
        if (!string.Equals(
                requestedTheme.LocalizationKey,
                defaultTheme.LocalizationKey,
                StringComparison.Ordinal) &&
            TryApplyTheme(defaultTheme, out _))
        {
            ReportThemeLoadFailure(requestedError!);
            return;
        }

        ReportThemeLoadFailure(requestedError!);
    }

    private bool TryApplyTheme(ThemeDefinition theme, out Exception? error)
    {
        try
        {
            // Указываем сборку явно: словарь принадлежит KID даже при размещении
            // приложения в другом WPF-хосте (например, интеграционной проверке).
            var themeUri = new Uri(
                $"/{typeof(App).Assembly.GetName().Name};component/{theme.ResourcePath}", UriKind.Relative);
            var themeDictionary = new ResourceDictionary { Source = themeUri };

            if (app.Resources == null)
                throw new InvalidOperationException("Application resources are not available.");

            ReplaceThemeDictionary(app.Resources, themeDictionary);

            CurrentTheme = theme;
            _ = asyncOperationErrorHandler.ExecuteAsync(
                () => windowConfigurationService.SetColorThemeAsync(theme.LocalizationKey),
                "Error_SettingsSaveFailed");
            ThemeChanged?.Invoke(this, EventArgs.Empty);

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    /// <summary>
    /// Заменяет только словарь внутри контейнера темы, сохраняя остальные ресурсы
    /// и порядок их поиска. Вызывается на UI-потоке после загрузки новой темы.
    /// </summary>
    internal static void ReplaceThemeDictionary(
        ResourceDictionary applicationResources,
        ResourceDictionary themeDictionary)
    {
        ArgumentNullException.ThrowIfNull(applicationResources);
        ArgumentNullException.ThrowIfNull(themeDictionary);

        ThemeResourceContainer? themeContainer = null;
        foreach (var dictionary in applicationResources.MergedDictionaries)
        {
            if (dictionary is not ThemeResourceContainer container)
                continue;

            if (themeContainer != null)
                throw new InvalidOperationException("Application resources must contain exactly one theme resource container.");

            themeContainer = container;
        }

        if (themeContainer == null)
            throw new InvalidOperationException("Application resources must contain exactly one theme resource container.");

        if (themeContainer.Source != null || themeContainer.Count != 0)
            throw new InvalidOperationException("The theme resource container must not have a source or direct resources.");

        if (themeContainer.MergedDictionaries.Count != 1)
            throw new InvalidOperationException("The theme resource container must contain exactly one active theme dictionary.");

        // Контейнер сохраняет свою позицию; заменяется только принадлежащая ему тема.
        themeContainer.MergedDictionaries[0] = themeDictionary;
    }

    private void ReportThemeLoadFailure(Exception exception)
    {
        _ = asyncOperationErrorHandler.ExecuteAsync(
            () => Task.FromException(exception),
            "Error_ThemeLoadFailed");
    }
}

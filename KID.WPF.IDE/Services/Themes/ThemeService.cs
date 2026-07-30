using System;
using System.Threading.Tasks;
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
        var migratedKey = LegacyThemeKeyMigrator.Migrate(localizationKey);
        var requestedTheme = themeProviderService.TryGetTheme(migratedKey, out var resolvedTheme)
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
            var themeUri = new Uri($"/{theme.ResourcePath}", UriKind.Relative);
            var themeDictionary = new ResourceDictionary { Source = themeUri };

            if (app.Resources == null)
                throw new InvalidOperationException("Application resources are not available.");

            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(themeDictionary);

            CurrentTheme = theme;
            windowConfigurationService.SetColorTheme(theme.LocalizationKey);
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

    private void ReportThemeLoadFailure(Exception exception)
    {
        _ = asyncOperationErrorHandler.ExecuteAsync(
            () => Task.FromException(exception),
            "Error_ThemeLoadFailed");
    }
}

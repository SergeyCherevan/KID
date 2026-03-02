using KID.Services.CodeEditor.Interfaces;
using KID.Services.Initialize.Interfaces;
using RoslynPad.Editor;

namespace KID.Services.CodeEditor;

/// <summary>
/// Провайдер палитры подсветки: возвращает светлую или тёмную палитру в зависимости от Settings.ColorTheme.
/// </summary>
public class ClassificationHighlightColorsProvider : IClassificationHighlightColorsProvider
{
    private static readonly ClassificationHighlightColors LightColors = new();
    private static readonly DarkClassificationHighlightColors DarkColors = new();

    private readonly IWindowConfigurationService _windowConfigurationService;

    /// <summary>
    /// Создаёт провайдер.
    /// </summary>
    public ClassificationHighlightColorsProvider(IWindowConfigurationService windowConfigurationService)
    {
        _windowConfigurationService = windowConfigurationService ?? throw new System.ArgumentNullException(nameof(windowConfigurationService));
    }

    /// <inheritdoc />
    public IClassificationHighlightColors GetColors() =>
        string.Equals(_windowConfigurationService.Settings.ColorTheme, "Dark", System.StringComparison.OrdinalIgnoreCase)
            ? DarkColors
            : LightColors;
}

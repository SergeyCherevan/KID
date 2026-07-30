using System;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.Themes;
using RoslynPad.Editor;

namespace KID.Services.CodeEditor;

/// <summary>
/// Возвращает палитру синтаксической подсветки, объявленную текущей XAML-темой.
/// </summary>
public sealed class ClassificationHighlightColorsProvider : IClassificationHighlightColorsProvider
{
    private static readonly ClassificationHighlightColors LightColors = new();
    private static readonly DarkClassificationHighlightColors DarkColors = new();

    private readonly App app;

    public ClassificationHighlightColorsProvider(App app)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <inheritdoc />
    public IClassificationHighlightColors GetColors() =>
        GetCurrentPaletteKind() switch
        {
            EditorPaletteKind.Dark => DarkColors,
            EditorPaletteKind.Light => LightColors,
            _ => LightColors
        };

    private EditorPaletteKind GetCurrentPaletteKind() =>
        app.TryFindResource(ThemeResourceKeys.CodeEditorPaletteKind) is EditorPaletteKind paletteKind
            ? paletteKind
            : EditorPaletteKind.Light;
}

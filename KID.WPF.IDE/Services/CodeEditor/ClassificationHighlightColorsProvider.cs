using System;
using KID.Services.CodeEditor.Interfaces;
using RoslynPad.Editor;
using KID.Services.Themes;

namespace KID.Services.CodeEditor;

/// <summary>
/// Возвращает палитру синтаксической подсветки, объявленную текущей XAML-темой.
/// </summary>
public sealed class ClassificationHighlightColorsProvider : IClassificationHighlightColorsProvider
{
    private static readonly ClassificationHighlightColors LightColors = new();

    private readonly App app;

    public ClassificationHighlightColorsProvider(App app)
    {
        this.app = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <inheritdoc />
    public IClassificationHighlightColors GetColors() =>
        app.TryFindResource(ThemeResourceKeys.CodeEditorClassificationColors)
            is IClassificationHighlightColors colors
                ? colors
                : LightColors;
}

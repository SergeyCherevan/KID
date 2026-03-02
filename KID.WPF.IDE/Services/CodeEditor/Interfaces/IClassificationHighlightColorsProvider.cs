using RoslynPad.Editor;

namespace KID.Services.CodeEditor.Interfaces;

/// <summary>
/// Возвращает палитру цветов синтаксической подсветки в зависимости от текущей темы (светлая/тёмная).
/// </summary>
public interface IClassificationHighlightColorsProvider
{
    /// <summary>
    /// Возвращает палитру для текущей темы из настроек окна.
    /// </summary>
    IClassificationHighlightColors GetColors();
}

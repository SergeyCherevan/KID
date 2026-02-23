using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis.Classification;
using RoslynPad.Editor;
using RoslynPad.Roslyn.Classification;

namespace KID.Services.CodeEditor;

/// <summary>
/// Палитра цветов синтаксической подсветки для тёмной темы (фон #1E1E1E), в духе Visual Studio Dark.
/// </summary>
public class DarkClassificationHighlightColors : IClassificationHighlightColors
{
    private static readonly Color DefaultForeground = Color.FromRgb(0xD4, 0xD4, 0xD4);
    private static readonly Color KeywordColor = Color.FromRgb(0x56, 0x9C, 0xD6);
    private static readonly Color TypeColor = Color.FromRgb(0x4E, 0xC9, 0xB0);
    private static readonly Color MethodColor = Color.FromRgb(0xDC, 0xDC, 0xAA);
    private static readonly Color ParameterColor = Color.FromRgb(0x9C, 0xDC, 0xFE);
    private static readonly Color CommentColor = Color.FromRgb(0x6A, 0x99, 0x55);
    private static readonly Color XmlCommentColor = Color.FromRgb(0x80, 0x80, 0x80);
    private static readonly Color PreprocessorColor = Color.FromRgb(0xC5, 0x86, 0xC0);
    private static readonly Color StringColor = Color.FromRgb(0xCE, 0x91, 0x78);
    private static readonly Color BraceMatchForeground = Color.FromRgb(0xD4, 0xD4, 0xD4);
    private static readonly Color BraceMatchBackground = Color.FromArgb(0x40, 0x60, 0x60, 0x60);

    /// <inheritdoc />
    public HighlightingColor DefaultBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(DefaultForeground)
    };

    /// <summary>Цвет для типов (классы, интерфейсы и т.д.).</summary>
    public HighlightingColor TypeBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(TypeColor)
    };

    /// <summary>Цвет для имён методов.</summary>
    public HighlightingColor MethodBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(MethodColor)
    };

    /// <summary>Цвет для имён параметров.</summary>
    public HighlightingColor ParameterBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(ParameterColor)
    };

    /// <summary>Цвет для комментариев.</summary>
    public HighlightingColor CommentBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(CommentColor)
    };

    /// <summary>Цвет для XML-документации.</summary>
    public HighlightingColor XmlCommentBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(XmlCommentColor)
    };

    /// <summary>Цвет для ключевых слов.</summary>
    public HighlightingColor KeywordBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(KeywordColor)
    };

    /// <summary>Цвет для директив препроцессора.</summary>
    public HighlightingColor PreprocessorKeywordBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(PreprocessorColor)
    };

    /// <summary>Цвет для строковых литералов.</summary>
    public HighlightingColor StringBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(StringColor)
    };

    /// <summary>Цвет подсветки парных скобок.</summary>
    public HighlightingColor BraceMatchingBrush { get; protected set; } = new HighlightingColor
    {
        Foreground = new SimpleHighlightingBrush(BraceMatchForeground),
        Background = new SimpleHighlightingBrush(BraceMatchBackground)
    };

    /// <summary>Стиль для статических символов (жирный).</summary>
    public HighlightingColor StaticSymbolBrush { get; protected set; } = new HighlightingColor
    {
        FontWeight = FontWeights.Bold,
        Foreground = new SimpleHighlightingBrush(DefaultForeground)
    };

    private readonly Lazy<ImmutableDictionary<string, HighlightingColor>> _map;

    /// <summary>
    /// Создаёт экземпляр палитры для тёмной темы.
    /// </summary>
    public DarkClassificationHighlightColors()
    {
        _map = new Lazy<ImmutableDictionary<string, HighlightingColor>>(() => new Dictionary<string, HighlightingColor>
        {
            [ClassificationTypeNames.ClassName] = TypeBrush,
            [ClassificationTypeNames.RecordClassName] = TypeBrush,
            [ClassificationTypeNames.RecordStructName] = TypeBrush,
            [ClassificationTypeNames.StructName] = TypeBrush,
            [ClassificationTypeNames.InterfaceName] = TypeBrush,
            [ClassificationTypeNames.DelegateName] = TypeBrush,
            [ClassificationTypeNames.EnumName] = TypeBrush,
            [ClassificationTypeNames.ModuleName] = TypeBrush,
            [ClassificationTypeNames.TypeParameterName] = TypeBrush,
            [ClassificationTypeNames.MethodName] = MethodBrush,
            [ClassificationTypeNames.ExtensionMethodName] = MethodBrush,
            [ClassificationTypeNames.ParameterName] = ParameterBrush,
            [ClassificationTypeNames.Comment] = CommentBrush,
            [ClassificationTypeNames.StaticSymbol] = StaticSymbolBrush,
            [ClassificationTypeNames.XmlDocCommentAttributeName] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentAttributeQuotes] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentAttributeValue] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentCDataSection] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentComment] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentDelimiter] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentEntityReference] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentName] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentProcessingInstruction] = XmlCommentBrush,
            [ClassificationTypeNames.XmlDocCommentText] = CommentBrush,
            [ClassificationTypeNames.Keyword] = KeywordBrush,
            [ClassificationTypeNames.ControlKeyword] = KeywordBrush,
            [ClassificationTypeNames.PreprocessorKeyword] = PreprocessorKeywordBrush,
            [ClassificationTypeNames.StringLiteral] = StringBrush,
            [ClassificationTypeNames.VerbatimStringLiteral] = StringBrush,
            [AdditionalClassificationTypeNames.BraceMatching] = BraceMatchingBrush
        }.ToImmutableDictionary());
    }

    /// <inheritdoc />
    public HighlightingColor GetBrush(string classificationTypeName)
    {
        _map.Value.TryGetValue(classificationTypeName, out var brush);
        return brush ?? DefaultBrush;
    }
}

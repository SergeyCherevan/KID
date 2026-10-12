using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis.Classification;
using RoslynPad.Editor;
using RoslynPad.Roslyn.Classification;

namespace KID.Services.CodeEditor;

/// <summary>
/// Цвета и начертания синтаксической подсветки Visual Studio 2022 (тёмная тема).
/// </summary>
public sealed class VS2022DarkClassificationHighlightColors : IClassificationHighlightColors
{
    private readonly ImmutableDictionary<string, HighlightingColor> _map;

    /// <inheritdoc />
    public HighlightingColor DefaultBrush { get; } = CreateBrush(0xDC, 0xDC, 0xDC);

    /// <summary>
    /// Создаёт палитру Visual Studio 2022 (тёмная тема).
    /// </summary>
    public VS2022DarkClassificationHighlightColors()
    {
        var regexSelfEscapedBrush = CreateBrush(0xD6, 0x9D, 0x85);
        regexSelfEscapedBrush.FontWeight = FontWeights.Bold;

        // Непубличные имена классификаций Roslyn заданы проверенными строковыми ключами.
        // Добавочные стили не задают Foreground, чтобы сохранить основной цвет символа.
        _map = new Dictionary<string, HighlightingColor>
        {
            [ClassificationTypeNames.Comment] = CreateBrush(0x57, 0xA6, 0x4A),
            [ClassificationTypeNames.ExcludedCode] = CreateBrush(0x9B, 0x9B, 0x9B),
            [ClassificationTypeNames.Identifier] = DefaultBrush,
            [ClassificationTypeNames.Keyword] = CreateBrush(0x56, 0x9C, 0xD6),
            [ClassificationTypeNames.ControlKeyword] = CreateBrush(0xD8, 0xA0, 0xDF),
            [ClassificationTypeNames.NumericLiteral] = CreateBrush(0xB5, 0xCE, 0xA8),
            [ClassificationTypeNames.Operator] = CreateBrush(0xB4, 0xB4, 0xB4),
            [ClassificationTypeNames.OperatorOverloaded] = CreateBrush(0xDC, 0xDC, 0xAA),
            [ClassificationTypeNames.PreprocessorKeyword] = CreateBrush(0x9B, 0x9B, 0x9B),
            [ClassificationTypeNames.StringLiteral] = CreateBrush(0xD6, 0x9D, 0x85),
            [ClassificationTypeNames.WhiteSpace] = DefaultBrush,
            [ClassificationTypeNames.Text] = DefaultBrush,
            ["reassigned variable"] = new HighlightingColor { Underline = true },
            ["obsolete symbol"] = new HighlightingColor { Strikethrough = true },
            [ClassificationTypeNames.StaticSymbol] = new HighlightingColor(),
            [ClassificationTypeNames.PreprocessorText] = CreateBrush(0xDC, 0xDC, 0xDC),
            [ClassificationTypeNames.Punctuation] = CreateBrush(0xDC, 0xDC, 0xDC),
            [ClassificationTypeNames.VerbatimStringLiteral] = CreateBrush(0xD6, 0x9D, 0x85),
            [ClassificationTypeNames.StringEscapeCharacter] = CreateBrush(0xFF, 0xD6, 0x8F),
            [ClassificationTypeNames.ClassName] = CreateBrush(0x4E, 0xC9, 0xB0),
            [ClassificationTypeNames.RecordClassName] = CreateBrush(0x4E, 0xC9, 0xB0),
            [ClassificationTypeNames.DelegateName] = CreateBrush(0x4E, 0xC9, 0xB0),
            [ClassificationTypeNames.EnumName] = CreateBrush(0xB8, 0xD7, 0xA3),
            [ClassificationTypeNames.InterfaceName] = CreateBrush(0xB8, 0xD7, 0xA3),
            [ClassificationTypeNames.ModuleName] = CreateBrush(0x4E, 0xC9, 0xB0),
            [ClassificationTypeNames.StructName] = CreateBrush(0x86, 0xC6, 0x91),
            [ClassificationTypeNames.RecordStructName] = CreateBrush(0x86, 0xC6, 0x91),
            [ClassificationTypeNames.TypeParameterName] = CreateBrush(0xB8, 0xD7, 0xA3),
            ["array name"] = CreateBrush(0x4E, 0xC9, 0xB0),
            ["pointer name"] = CreateBrush(0x86, 0xC6, 0x91),
            ["function pointer name"] = CreateBrush(0x86, 0xC6, 0x91),
            [ClassificationTypeNames.FieldName] = DefaultBrush,
            [ClassificationTypeNames.EnumMemberName] = DefaultBrush,
            [ClassificationTypeNames.ConstantName] = DefaultBrush,
            [ClassificationTypeNames.LocalName] = CreateBrush(0x9C, 0xDC, 0xFE),
            [ClassificationTypeNames.ParameterName] = CreateBrush(0x9C, 0xDC, 0xFE),
            [ClassificationTypeNames.MethodName] = CreateBrush(0xDC, 0xDC, 0xAA),
            [ClassificationTypeNames.ExtensionMethodName] = CreateBrush(0xDC, 0xDC, 0xAA),
            [ClassificationTypeNames.PropertyName] = DefaultBrush,
            [ClassificationTypeNames.EventName] = DefaultBrush,
            [ClassificationTypeNames.NamespaceName] = DefaultBrush,
            [ClassificationTypeNames.LabelName] = DefaultBrush,
            [ClassificationTypeNames.XmlDocCommentAttributeName] = CreateBrush(0xC8, 0xC8, 0xC8),
            [ClassificationTypeNames.XmlDocCommentAttributeQuotes] = CreateBrush(0xC8, 0xC8, 0xC8),
            [ClassificationTypeNames.XmlDocCommentAttributeValue] = CreateBrush(0xC8, 0xC8, 0xC8),
            [ClassificationTypeNames.XmlDocCommentCDataSection] = CreateBrush(0xE9, 0xD5, 0x85),
            [ClassificationTypeNames.XmlDocCommentComment] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlDocCommentDelimiter] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlDocCommentEntityReference] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlDocCommentName] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlDocCommentProcessingInstruction] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlDocCommentText] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlLiteralAttributeName] = CreateBrush(0xC8, 0xC8, 0xC8),
            [ClassificationTypeNames.XmlLiteralAttributeQuotes] = CreateBrush(0x92, 0xCA, 0xF4),
            [ClassificationTypeNames.XmlLiteralAttributeValue] = CreateBrush(0xC8, 0xC8, 0xC8),
            [ClassificationTypeNames.XmlLiteralCDataSection] = CreateBrush(0xE9, 0xD5, 0x85),
            [ClassificationTypeNames.XmlLiteralComment] = CreateBrush(0x60, 0x8B, 0x4E),
            [ClassificationTypeNames.XmlLiteralDelimiter] = CreateBrush(0x89, 0xBB, 0x82),
            [ClassificationTypeNames.XmlLiteralEmbeddedExpression] = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x71, 0x71, 0x71)), Background = new SimpleHighlightingBrush(Color.FromRgb(0xED, 0xE0, 0xD1)) },
            [ClassificationTypeNames.XmlLiteralEntityReference] = CreateBrush(0x92, 0xCA, 0xF4),
            [ClassificationTypeNames.XmlLiteralName] = CreateBrush(0x56, 0x9C, 0xD6),
            [ClassificationTypeNames.XmlLiteralProcessingInstruction] = CreateBrush(0xAE, 0xAE, 0xAE),
            [ClassificationTypeNames.XmlLiteralText] = CreateBrush(0xC8, 0xC8, 0xC8),
            [ClassificationTypeNames.RegexComment] = CreateBrush(0x57, 0xA6, 0x4A),
            [ClassificationTypeNames.RegexCharacterClass] = CreateBrush(0x2E, 0xAB, 0xFE),
            [ClassificationTypeNames.RegexAnchor] = CreateBrush(0xF9, 0x79, 0xAE),
            [ClassificationTypeNames.RegexQuantifier] = CreateBrush(0xF9, 0x79, 0xAE),
            [ClassificationTypeNames.RegexGrouping] = CreateBrush(0x05, 0xC3, 0xBA),
            [ClassificationTypeNames.RegexAlternation] = CreateBrush(0x05, 0xC3, 0xBA),
            [ClassificationTypeNames.RegexText] = CreateBrush(0xD6, 0x9D, 0x85),
            [ClassificationTypeNames.RegexSelfEscapedCharacter] = regexSelfEscapedBrush,
            [ClassificationTypeNames.RegexOtherEscape] = CreateBrush(0xFF, 0xD6, 0x8F),
            ["json - comment"] = CreateBrush(0x57, 0xA6, 0x4A),
            ["json - number"] = CreateBrush(0xB5, 0xCE, 0xA8),
            ["json - string"] = CreateBrush(0xD6, 0x9D, 0x85),
            ["json - keyword"] = CreateBrush(0x56, 0x9C, 0xD6),
            ["json - text"] = CreateBrush(0xDC, 0xDC, 0xDC),
            ["json - operator"] = CreateBrush(0xB4, 0xB4, 0xB4),
            ["json - punctuation"] = CreateBrush(0xDC, 0xDC, 0xDC),
            ["json - array"] = CreateBrush(0xDC, 0xDC, 0xDC),
            ["json - object"] = CreateBrush(0xDC, 0xDC, 0xDC),
            ["json - property name"] = CreateBrush(0xDC, 0xDC, 0xAA),
            ["json - constructor name"] = CreateBrush(0x86, 0xC6, 0x91),
            [AdditionalClassificationTypeNames.BraceMatching] = new HighlightingColor { Background = new SimpleHighlightingBrush(Color.FromRgb(0x0E, 0x45, 0x83)) },
        }.ToImmutableDictionary(StringComparer.Ordinal);

        DefaultBrush.Freeze();
        foreach (var brush in _map.Values)
            brush.Freeze();
    }

    /// <inheritdoc />
    public HighlightingColor GetBrush(string classificationTypeName) =>
        _map.TryGetValue(classificationTypeName, out var brush) ? brush : DefaultBrush;

    private static HighlightingColor CreateBrush(byte red, byte green, byte blue) => new()
    {
        Foreground = new SimpleHighlightingBrush(Color.FromRgb(red, green, blue))
    };
}

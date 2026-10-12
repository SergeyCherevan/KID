using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis.Classification;
using RoslynPad.Editor;
using RoslynPad.Roslyn.Classification;

namespace KID.Services.CodeEditor;

/// <summary>
/// Цвета и начертания синтаксической подсветки Visual Studio 2022 (светлая тема).
/// </summary>
public sealed class VS2022LightClassificationHighlightColors : IClassificationHighlightColors
{
    private readonly ImmutableDictionary<string, HighlightingColor> _map;

    /// <inheritdoc />
    public HighlightingColor DefaultBrush { get; } = CreateBrush(0x00, 0x00, 0x00);

    /// <summary>
    /// Создаёт палитру Visual Studio 2022 (светлая тема).
    /// </summary>
    public VS2022LightClassificationHighlightColors()
    {
        var regexSelfEscapedBrush = CreateBrush(0x80, 0x00, 0x00);
        regexSelfEscapedBrush.FontWeight = FontWeights.Bold;

        // Непубличные имена классификаций Roslyn заданы проверенными строковыми ключами.
        // Добавочные стили не задают Foreground, чтобы сохранить основной цвет символа.
        _map = new Dictionary<string, HighlightingColor>
        {
            [ClassificationTypeNames.Comment] = CreateBrush(0x00, 0x80, 0x00),
            [ClassificationTypeNames.ExcludedCode] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.Identifier] = DefaultBrush,
            [ClassificationTypeNames.Keyword] = CreateBrush(0x00, 0x00, 0xFF),
            [ClassificationTypeNames.ControlKeyword] = CreateBrush(0x8F, 0x08, 0xC4),
            [ClassificationTypeNames.NumericLiteral] = CreateBrush(0x00, 0x00, 0x00),
            [ClassificationTypeNames.Operator] = CreateBrush(0x00, 0x00, 0x00),
            [ClassificationTypeNames.OperatorOverloaded] = CreateBrush(0x74, 0x53, 0x1F),
            [ClassificationTypeNames.PreprocessorKeyword] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.StringLiteral] = CreateBrush(0xA3, 0x15, 0x15),
            [ClassificationTypeNames.WhiteSpace] = DefaultBrush,
            [ClassificationTypeNames.Text] = DefaultBrush,
            ["reassigned variable"] = new HighlightingColor { Underline = true },
            ["obsolete symbol"] = new HighlightingColor { Strikethrough = true },
            [ClassificationTypeNames.StaticSymbol] = new HighlightingColor(),
            [ClassificationTypeNames.PreprocessorText] = CreateBrush(0x00, 0x00, 0x00),
            [ClassificationTypeNames.Punctuation] = CreateBrush(0x00, 0x00, 0x00),
            [ClassificationTypeNames.VerbatimStringLiteral] = CreateBrush(0x80, 0x00, 0x00),
            [ClassificationTypeNames.StringEscapeCharacter] = CreateBrush(0xB7, 0x76, 0xFB),
            [ClassificationTypeNames.ClassName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.RecordClassName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.DelegateName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.EnumName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.InterfaceName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.ModuleName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.StructName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.RecordStructName] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.TypeParameterName] = CreateBrush(0x2B, 0x91, 0xAF),
            ["array name"] = CreateBrush(0x2B, 0x91, 0xAF),
            ["pointer name"] = CreateBrush(0x2B, 0x91, 0xAF),
            ["function pointer name"] = CreateBrush(0x2B, 0x91, 0xAF),
            [ClassificationTypeNames.FieldName] = DefaultBrush,
            [ClassificationTypeNames.EnumMemberName] = DefaultBrush,
            [ClassificationTypeNames.ConstantName] = DefaultBrush,
            [ClassificationTypeNames.LocalName] = CreateBrush(0x1F, 0x37, 0x7F),
            [ClassificationTypeNames.ParameterName] = CreateBrush(0x1F, 0x37, 0x7F),
            [ClassificationTypeNames.MethodName] = CreateBrush(0x74, 0x53, 0x1F),
            [ClassificationTypeNames.ExtensionMethodName] = CreateBrush(0x74, 0x53, 0x1F),
            [ClassificationTypeNames.PropertyName] = DefaultBrush,
            [ClassificationTypeNames.EventName] = DefaultBrush,
            [ClassificationTypeNames.NamespaceName] = DefaultBrush,
            [ClassificationTypeNames.LabelName] = DefaultBrush,
            [ClassificationTypeNames.XmlDocCommentAttributeName] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentAttributeQuotes] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentAttributeValue] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentCDataSection] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentComment] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentDelimiter] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentEntityReference] = CreateBrush(0x00, 0x80, 0x00),
            [ClassificationTypeNames.XmlDocCommentName] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentProcessingInstruction] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlDocCommentText] = CreateBrush(0x00, 0x80, 0x00),
            [ClassificationTypeNames.XmlLiteralAttributeName] = CreateBrush(0xB9, 0x64, 0x64),
            [ClassificationTypeNames.XmlLiteralAttributeQuotes] = CreateBrush(0x55, 0x55, 0x55),
            [ClassificationTypeNames.XmlLiteralAttributeValue] = CreateBrush(0x64, 0x64, 0xB9),
            [ClassificationTypeNames.XmlLiteralCDataSection] = CreateBrush(0x80, 0x80, 0x80),
            [ClassificationTypeNames.XmlLiteralComment] = CreateBrush(0x62, 0x97, 0x55),
            [ClassificationTypeNames.XmlLiteralDelimiter] = CreateBrush(0x64, 0x64, 0xB9),
            [ClassificationTypeNames.XmlLiteralEmbeddedExpression] = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x55, 0x55, 0x55)), Background = new SimpleHighlightingBrush(Color.FromRgb(0xFF, 0xFE, 0xBF)) },
            [ClassificationTypeNames.XmlLiteralEntityReference] = CreateBrush(0xB9, 0x64, 0x64),
            [ClassificationTypeNames.XmlLiteralName] = CreateBrush(0x84, 0x46, 0x46),
            [ClassificationTypeNames.XmlLiteralProcessingInstruction] = CreateBrush(0xC0, 0xC0, 0xC0),
            [ClassificationTypeNames.XmlLiteralText] = CreateBrush(0x55, 0x55, 0x55),
            [ClassificationTypeNames.RegexComment] = CreateBrush(0x00, 0x80, 0x00),
            [ClassificationTypeNames.RegexCharacterClass] = CreateBrush(0x00, 0x73, 0xFF),
            [ClassificationTypeNames.RegexAnchor] = CreateBrush(0xFF, 0x00, 0xC1),
            [ClassificationTypeNames.RegexQuantifier] = CreateBrush(0xFF, 0x00, 0xC1),
            [ClassificationTypeNames.RegexGrouping] = CreateBrush(0x05, 0xC3, 0xBA),
            [ClassificationTypeNames.RegexAlternation] = CreateBrush(0x05, 0xC3, 0xBA),
            [ClassificationTypeNames.RegexText] = CreateBrush(0x80, 0x00, 0x00),
            [ClassificationTypeNames.RegexSelfEscapedCharacter] = regexSelfEscapedBrush,
            [ClassificationTypeNames.RegexOtherEscape] = CreateBrush(0x9E, 0x5B, 0x71),
            ["json - comment"] = CreateBrush(0x00, 0x80, 0x00),
            ["json - number"] = CreateBrush(0x00, 0x00, 0x00),
            ["json - string"] = CreateBrush(0xA3, 0x15, 0x15),
            ["json - keyword"] = CreateBrush(0x00, 0x00, 0xFF),
            ["json - text"] = CreateBrush(0x00, 0x00, 0x00),
            ["json - operator"] = CreateBrush(0x00, 0x00, 0x00),
            ["json - punctuation"] = CreateBrush(0x00, 0x00, 0x00),
            ["json - array"] = CreateBrush(0x00, 0x00, 0x00),
            ["json - object"] = CreateBrush(0x00, 0x00, 0x00),
            ["json - property name"] = CreateBrush(0x74, 0x53, 0x1F),
            ["json - constructor name"] = CreateBrush(0x2B, 0x91, 0xAF),
            [AdditionalClassificationTypeNames.BraceMatching] = new HighlightingColor { Background = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xE0, 0xCC)) },
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

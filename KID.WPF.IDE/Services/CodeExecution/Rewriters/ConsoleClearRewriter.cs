using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Заменяет мостом WPF-консоли только вызов BCL <see cref="Console.Clear"/>.
/// </summary>
internal sealed class ConsoleClearRewriter : CSharpSyntaxRewriter
{
    private const string ConsoleClearTarget =
        "global::KID.Services.CodeExecution.TextBoxConsole.StaticConsole.Clear";

    private readonly SemanticModel semanticModel;
    private readonly CancellationToken cancellationToken;
    private readonly INamedTypeSymbol? systemConsoleType;

    public ConsoleClearRewriter(
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        this.semanticModel = semanticModel ??
            throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;
        systemConsoleType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Console));
    }

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return base.Visit(node);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var method = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        if (!IsSystemConsoleClear(method))
            return rewritten;

        var target = SyntaxFactory.ParseExpression(ConsoleClearTarget)
            .WithTriviaFrom(rewritten.Expression);
        return rewritten.WithExpression(target);
    }

    private bool IsSystemConsoleClear(IMethodSymbol? method) =>
        method is
        {
            Name: nameof(Console.Clear),
            IsStatic: true,
            Parameters.Length: 0
        } &&
        systemConsoleType is not null &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, systemConsoleType);
}

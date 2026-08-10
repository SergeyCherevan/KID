using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Добавляет в пользовательский код точки кооперативной проверки Stop,
/// не изменяя публичные API KID. Семантические преобразования выполняются
/// только для разрешённых и однозначно определённых символов BCL.
/// </summary>
internal sealed class CancellationInstrumentationRewriter : CSharpSyntaxRewriter
{
    private const string StopCheckCode =
        "global::KID.StopManager.StopIfButtonPressed();";
    private const string CurrentTokenCode =
        "global::KID.StopManager.CurrentToken";
    private const string SleepTargetCode =
        "global::KID.StopManager.Sleep";

    private readonly SemanticModel semanticModel;
    private readonly CancellationToken cancellationToken;
    private readonly INamedTypeSymbol? systemThreadType;
    private readonly INamedTypeSymbol? systemTaskType;

    public CancellationInstrumentationRewriter(
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        this.semanticModel = semanticModel ??
            throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;
        systemThreadType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Thread));
        systemTaskType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Task));
    }

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return base.Visit(node);
    }

    // Пользовательские finally и финализатор являются путями очистки ресурсов.
    // Вставка отмены могла бы прервать очистку, поэтому намеренно не входит в этот этап.
    public override SyntaxNode? VisitFinallyClause(FinallyClauseSyntax node) => node;

    public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) => node;

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var rewritten = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
        var members = new List<MemberDeclarationSyntax>(rewritten.Members.Count + 1);
        var seenGlobalStatement = false;

        foreach (var member in rewritten.Members)
        {
            if (member is not GlobalStatementSyntax globalStatement)
            {
                members.Add(member);
                continue;
            }

            if (!seenGlobalStatement && !IsStopCheck(globalStatement.Statement))
                members.Add(SyntaxFactory.GlobalStatement(CreateStopCheck()));

            if (NeedsContinuationCheck(globalStatement.Statement) &&
                (members.LastOrDefault() is not GlobalStatementSyntax previous ||
                 !IsStopCheck(previous.Statement)))
            {
                members.Add(SyntaxFactory.GlobalStatement(CreateStopCheck()));
            }

            members.Add(globalStatement);
            seenGlobalStatement = true;
        }

        return rewritten.WithMembers(SyntaxFactory.List(members));
    }

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var rewritten = (BlockSyntax)base.VisitBlock(node)!;
        return rewritten.WithStatements(AddContinuationChecks(rewritten.Statements));
    }

    public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
    {
        var rewritten = (SwitchSectionSyntax)base.VisitSwitchSection(node)!;
        return rewritten.WithStatements(AddContinuationChecks(rewritten.Statements));
    }

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var rewritten = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var rewritten = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var rewritten = (ForStatementSyntax)base.VisitForStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var rewritten = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        var rewritten = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        var rewritten = (IfStatementSyntax)base.VisitIfStatement(node)!;
        var rewrittenElse = rewritten.Else is null
            ? null
            : rewritten.Else.WithStatement(
                AddEmbeddedContinuationCheckpoint(rewritten.Else.Statement));

        return rewritten
            .WithStatement(AddEmbeddedContinuationCheckpoint(rewritten.Statement))
            .WithElse(rewrittenElse);
    }

    public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
    {
        var rewritten = (UsingStatementSyntax)base.VisitUsingStatement(node)!;
        return rewritten.WithStatement(
            AddEmbeddedContinuationCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitLabeledStatement(LabeledStatementSyntax node)
    {
        var rewritten = (LabeledStatementSyntax)base.VisitLabeledStatement(node)!;
        return rewritten.WithStatement(
            AddEmbeddedContinuationCheckpoint(rewritten.Statement));
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var expressionBehavior = GetMethodExpressionBehavior(node);
        var rewritten = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        if (rewritten.ExpressionBody is null || expressionBehavior is null)
            return rewritten;

        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                expressionBehavior.Value))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var expressionBehavior = GetMethodExpressionBehavior(node);
        var rewritten = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;

        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        if (rewritten.ExpressionBody is null || expressionBehavior is null)
            return rewritten;

        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                expressionBehavior.Value))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;

        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Expression))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;

        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Return))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitConversionOperatorDeclaration(
        ConversionOperatorDeclarationSyntax node)
    {
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (ConversionOperatorDeclarationSyntax)
            base.VisitConversionOperatorDeclaration(node)!;

        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Return))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;

        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        var behavior = rewritten.Kind() == SyntaxKind.GetAccessorDeclaration
            ? ExpressionBodyBehavior.Return
            : ExpressionBodyBehavior.Expression;

        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                behavior))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        var rewritten = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(
        ParenthesizedLambdaExpressionSyntax node)
    {
        var rewritten = (ParenthesizedLambdaExpressionSyntax)
            base.VisitParenthesizedLambdaExpression(node)!;

        return rewritten.Block is null
            ? rewritten
            : rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var rewritten = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
        return rewritten.Block is null
            ? rewritten
            : rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var method = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        if (IsThreadSleep(method) && CanReplaceInvocationTarget(node.Expression))
        {
            var target = SyntaxFactory.ParseExpression(SleepTargetCode)
                .WithTriviaFrom(rewritten.Expression);
            return rewritten.WithExpression(target);
        }

        if (IsSingleArgumentTaskDelay(method))
        {
            var tokenArgument = SyntaxFactory.Argument(
                    SyntaxFactory.ParseExpression(CurrentTokenCode))
                .WithNameColon(SyntaxFactory.NameColon("cancellationToken"));
            return rewritten.WithArgumentList(
                rewritten.ArgumentList.WithArguments(
                    rewritten.ArgumentList.Arguments.Add(tokenArgument)));
        }

        return rewritten;
    }

    private ExpressionBodyBehavior? GetMethodExpressionBehavior(SyntaxNode node)
    {
        var expressionBody = node switch
        {
            MethodDeclarationSyntax method => method.ExpressionBody,
            LocalFunctionStatementSyntax localFunction => localFunction.ExpressionBody,
            _ => null
        };

        if (!CanConvertExpressionBody(expressionBody))
            return null;

        var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken) as IMethodSymbol;
        if (symbol is null)
            return null;

        if (symbol.ReturnsVoid)
            return ExpressionBodyBehavior.Expression;

        if (!symbol.IsAsync)
            return ExpressionBodyBehavior.Return;

        if (IsKnownTaskLike(symbol.ReturnType, arity: 0))
            return ExpressionBodyBehavior.Expression;

        if (IsKnownTaskLike(symbol.ReturnType, arity: 1))
            return ExpressionBodyBehavior.Return;

        // Пользовательские task-like типы требуют преобразования с учётом
        // AsyncMethodBuilder, поэтому их expression body намеренно не переписывается.
        return null;
    }

    private static bool IsKnownTaskLike(ITypeSymbol returnType, int arity) =>
        returnType is INamedTypeSymbol namedType &&
        namedType.Arity == arity &&
        namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        namedType.Name is "Task" or "ValueTask";

    private static bool CanConvertExpressionBody(ArrowExpressionClauseSyntax? expressionBody) =>
        expressionBody is not null &&
        !expressionBody.ContainsDirectives &&
        !expressionBody.ArrowToken.LeadingTrivia.Any(IsDirectiveTrivia) &&
        !expressionBody.ArrowToken.TrailingTrivia.Any(IsDirectiveTrivia);

    private static bool IsDirectiveTrivia(SyntaxTrivia trivia) =>
        trivia.HasStructure && trivia.GetStructure() is DirectiveTriviaSyntax;

    private static BlockSyntax CreateExpressionBody(
        ArrowExpressionClauseSyntax expressionBody,
        SyntaxToken semicolonToken,
        ExpressionBodyBehavior behavior)
    {
        var statementSemicolon = semicolonToken.WithTrailingTrivia();
        StatementSyntax expressionStatement = expressionBody.Expression switch
        {
            ThrowExpressionSyntax throwExpression =>
                SyntaxFactory.ThrowStatement(throwExpression.Expression)
                    .WithThrowKeyword(
                        SyntaxFactory.Token(SyntaxKind.ThrowKeyword)
                            .WithTrailingTrivia(SyntaxFactory.Space))
                    .WithSemicolonToken(statementSemicolon),
            _ when behavior == ExpressionBodyBehavior.Return =>
                SyntaxFactory.ReturnStatement(expressionBody.Expression)
                    .WithReturnKeyword(
                        SyntaxFactory.Token(SyntaxKind.ReturnKeyword)
                            .WithTrailingTrivia(SyntaxFactory.Space))
                    .WithSemicolonToken(statementSemicolon),
            _ => SyntaxFactory.ExpressionStatement(expressionBody.Expression)
                .WithSemicolonToken(statementSemicolon)
        };

        var openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
            .WithLeadingTrivia(expressionBody.ArrowToken.LeadingTrivia)
            .WithTrailingTrivia(expressionBody.ArrowToken.TrailingTrivia);

        return SyntaxFactory.Block(CreateStopCheck(), expressionStatement)
            .WithOpenBraceToken(openBrace)
            .WithCloseBraceToken(
                SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithTrailingTrivia(semicolonToken.TrailingTrivia));
    }

    private static BlockSyntax AddEntryCheckpoint(BlockSyntax block)
    {
        if (block.Statements.FirstOrDefault() is { } firstStatement &&
            IsStopCheck(firstStatement))
        {
            return block;
        }

        return block.WithStatements(block.Statements.Insert(0, CreateStopCheck()));
    }

    private static StatementSyntax AddLoopCheckpoint(StatementSyntax statement)
    {
        if (statement is BlockSyntax block)
            return AddEntryCheckpoint(block);

        if (IsStopCheck(statement))
            return statement;

        return SyntaxFactory.Block(CreateStopCheck(), statement);
    }

    private static StatementSyntax AddEmbeddedContinuationCheckpoint(
        StatementSyntax statement)
    {
        if (statement is BlockSyntax || !NeedsContinuationCheck(statement))
            return statement;

        return SyntaxFactory.Block(CreateStopCheck(), statement);
    }

    private static SyntaxList<StatementSyntax> AddContinuationChecks(
        SyntaxList<StatementSyntax> statements)
    {
        var rewritten = new List<StatementSyntax>(statements.Count);

        foreach (var statement in statements)
        {
            if (NeedsContinuationCheck(statement) &&
                (rewritten.LastOrDefault() is not { } previous || !IsStopCheck(previous)))
            {
                rewritten.Add(CreateStopCheck());
            }

            rewritten.Add(statement);
        }

        return SyntaxFactory.List(rewritten);
    }

    private static bool NeedsContinuationCheck(StatementSyntax statement)
    {
        if (statement is YieldStatementSyntax)
            return true;

        if (statement is LocalDeclarationStatementSyntax localDeclaration &&
            localDeclaration.AwaitKeyword != default)
        {
            return true;
        }

        if (statement is UsingStatementSyntax usingStatement &&
            usingStatement.AwaitKeyword != default)
        {
            return true;
        }

        if (statement is not ExpressionStatementSyntax and
            not LocalDeclarationStatementSyntax and
            not ReturnStatementSyntax and
            not ThrowStatementSyntax)
        {
            return false;
        }

        return statement.DescendantNodes(
                ShouldDescendIntoContinuationCandidate)
            .OfType<AwaitExpressionSyntax>()
            .Any();
    }

    private static bool ShouldDescendIntoContinuationCandidate(SyntaxNode node) =>
        node is not AnonymousFunctionExpressionSyntax and
        not LocalFunctionStatementSyntax;

    private static bool IsStopCheck(StatementSyntax statement) =>
        statement.IsEquivalentTo(CreateStopCheck(), topLevel: false);

    private static ExpressionStatementSyntax CreateStopCheck() =>
        (ExpressionStatementSyntax)SyntaxFactory.ParseStatement(StopCheckCode);

    private static bool CanReplaceInvocationTarget(ExpressionSyntax expression) =>
        !expression.DescendantTrivia(descendIntoTrivia: true)
            .Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia) ||
                           trivia.IsDirective ||
                           trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

    private bool IsThreadSleep(IMethodSymbol? method) =>
        method is
        {
            Name: "Sleep",
            IsStatic: true,
            Parameters.Length: 1
        } &&
        systemThreadType is not null &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, systemThreadType) &&
        IsSupportedDelayParameter(method.Parameters[0].Type);

    private bool IsSingleArgumentTaskDelay(IMethodSymbol? method) =>
        method is
        {
            Name: "Delay",
            IsStatic: true,
            Parameters.Length: 1
        } &&
        systemTaskType is not null &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, systemTaskType) &&
        IsSupportedDelayParameter(method.Parameters[0].Type);

    private static bool IsSupportedDelayParameter(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_Int32 ||
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
        "global::System.TimeSpan";

    private enum ExpressionBodyBehavior
    {
        Expression,
        Return
    }
}

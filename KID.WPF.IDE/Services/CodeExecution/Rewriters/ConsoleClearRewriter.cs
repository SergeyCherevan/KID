using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Заменяет подтверждённый вызов BCL <see cref="Console.Clear"/> публичным мостом
/// WPF-консоли KID, не изменяя одноимённые пользовательские типы и методы.
/// </summary>
/// <remarks>
/// <para>
/// Класс является узкоспециализированным этапом transformation pipeline и использует
/// инфраструктуру <b>Visitor</b> через <see cref="CSharpSyntaxRewriter"/>. Visitor создаёт новое
/// immutable syntax tree, а исходное дерево и его <see cref="SemanticModel"/> остаются неизменными.
/// </para>
/// <para>
/// Решение о замене принимается по <see cref="IMethodSymbol"/>, а не по тексту выражения.
/// Дополнительно проверяется assembly identity типа <see cref="Console"/> через
/// <see cref="RuntimeTypeSymbolResolver"/>, поэтому исходный класс с metadata name
/// <c>System.Console</c> не может ошибочно считаться runtime BCL-типом.
/// </para>
/// <para>
/// Rewriter заменяет только target invocation и переносит на него исходные trivia. Аргументы,
/// surrounding statements, директивы и остальная структура программы обходятся стандартным
/// механизмом Roslyn без специальных изменений.
/// </para>
/// </remarks>
internal sealed class ConsoleClearRewriter : CSharpSyntaxRewriter
{
    // Fully-qualified target исключает зависимость сгенерированного кода от using directives
    // пользователя и направляет Clear в TextBoxConsole, инициализированный execution-контекстом.
    private const string ConsoleClearTarget =
        "global::KID.Services.CodeExecution.TextBoxConsole.StaticConsole.Clear";

    // SemanticModel принадлежит ровно тому SyntaxTree, узлы которого передаются Visitor.
    // Token связывает обход дерева с cooperative cancellation текущей execution-сессии.
    private readonly SemanticModel semanticModel;
    private readonly CancellationToken cancellationToken;

    // Runtime-resolved symbol фиксирует не только metadata name, но и identity сборки
    // System.Console, защищая transformation от source-defined shadow types.
    private readonly INamedTypeSymbol? systemConsoleType;

    /// <summary>
    /// Создаёт semantic-aware Visitor для одного конкретного syntax tree.
    /// </summary>
    /// <param name="semanticModel">
    /// Модель Roslyn, построенная для дерева, которое будет передано в <see cref="Visit"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен execution-сессии, проверяемый при посещении каждого syntax node и semantic lookup.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="semanticModel"/> имеет значение <see langword="null"/>.
    /// </exception>
    public ConsoleClearRewriter(
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        /* Visitor без SemanticModel не может отличить настоящий Console.Clear от метода с тем же
         * текстовым именем. Fail-fast сохраняет этот semantic safety invariant на границе класса.
         */
        this.semanticModel = semanticModel ??
            throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;

        /* Runtime type разрешается один раз для всего прохода, а не для каждой invocation.
         * null означает, что compilation не содержит подходящей runtime reference; в этом случае
         * rewriter безопасно оставит все вызовы неизменными.
         */
        systemConsoleType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Console));
    }

    /// <summary>
    /// Проверяет session token перед стандартным посещением каждого syntax node.
    /// </summary>
    /// <remarks>
    /// Override образует единую cancellation point для всего рекурсивного Visitor traversal.
    /// Конкретные Visit*-методы не обязаны дублировать проверку перед каждым дочерним узлом.
    /// </remarks>
    /// <param name="node">Текущий узел либо <see langword="null"/> по контракту Roslyn.</param>
    /// <returns>
    /// Переписанный узел, исходный узел без изменений либо <see langword="null"/>, если это
    /// допускает базовый Visitor.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Для execution-сессии уже запрошен Stop.
    /// </exception>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        /* Проверка выполняется до descent в node: после Stop Visitor не начинает обход нового
         * поддерева и быстро разматывает recursion стандартным cancellation exception.
         */
        cancellationToken.ThrowIfCancellationRequested();
        return base.Visit(node);
    }

    /// <summary>
    /// Семантически анализирует invocation и при точном совпадении заменяет target
    /// <see cref="Console.Clear"/> мостом WPF-консоли.
    /// </summary>
    /// <param name="node">Исходный invocation node из дерева связанной semantic model.</param>
    /// <returns>Переписанный invocation с прежними arguments/trivia либо неизменённый вызов.</returns>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        /* Symbol запрашивается у исходного node до recursive rewrite: SemanticModel привязана
         * именно к исходному tree и не обязана знать новые узлы, созданные base Visitor.
         */
        var method = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;

        /* Сначала переписываются вложенные invocation expressions. Полученный node используется
         * как structural base, чтобы изменения descendants не потерялись при замене outer target.
         */
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        /* Неоднозначный, неразрешённый, пользовательский или неподходящий overload остаётся
         * полностью неизменным после обычного recursive traversal.
         */
        if (!IsSystemConsoleClear(method))
            return rewritten;

        /* ParseExpression создаёт fully-qualified target без зависимости от пользовательских
         * aliases/usings. WithTriviaFrom сохраняет комментарии и пробелы исходного expression,
         * а WithExpression оставляет argument list и окружающий invocation node прежними.
         */
        var target = SyntaxFactory.ParseExpression(ConsoleClearTarget)
            .WithTriviaFrom(rewritten.Expression);
        return rewritten.WithExpression(target);
    }

    /// <summary>
    /// Проверяет точную сигнатуру и runtime type identity метода BCL Console.Clear().
    /// </summary>
    /// <param name="method">Разрешённый Roslyn method symbol либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> только для статического безаргументного метода Clear настоящего
    /// runtime-типа <see cref="Console"/>; иначе <see langword="false"/>.
    /// </returns>
    private bool IsSystemConsoleClear(IMethodSymbol? method) =>
        /* Property pattern сначала дешёво проверяет форму метода. SymbolEqualityComparer затем
         * сравнивает Roslyn symbols с учётом compilation identity, а не строкового display name.
         */
        method is
        {
            Name: nameof(Console.Clear),
            IsStatic: true,
            Parameters.Length: 0
        } &&
        systemConsoleType is not null &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, systemConsoleType);
}

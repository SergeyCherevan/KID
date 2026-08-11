using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Добавляет в пользовательский код точки кооперативной проверки Stop,
/// не изменяя публичные API KID. Семантические преобразования выполняются
/// только для разрешённых и однозначно определённых символов BCL.
/// </summary>
/// <remarks>
/// <para>
/// Класс реализует syntax-tree <b>Visitor / Rewriter</b> на базе
/// <see cref="CSharpSyntaxRewriter"/> и является первым transformation stage компилятора KID.
/// Он создаёт новое immutable дерево, в котором обычная учебная программа периодически вызывает
/// стабильную cancellation point <see cref="global::KID.StopManager.StopIfButtonPressed"/>.
/// </para>
/// <para>
/// Структурные checkpoints добавляются на входе в поддержанные циклы и callable bodies, а также
/// перед statement-level <see langword="await"/> и <see langword="yield"/>. Это не принудительное
/// завершение потока, а <b>Cooperative Cancellation</b>: выполнение останавливается только после
/// достижения одной из вставленных или явно поддержанных точек наблюдения token.
/// </para>
/// <para>
/// Преобразования <see cref="Thread.Sleep(int)"/> и <see cref="Task.Delay(int)"/> дополнительно
/// используют <see cref="SemanticModel"/> и runtime assembly identity. Поэтому пользовательские
/// методы с именами <c>Sleep</c>/<c>Delay</c>, source-defined shadow types и custom awaitables не
/// переписываются только из-за текстового сходства с BCL.
/// </para>
/// <para>
/// Rewriter придерживается fail-closed правила: если корректность преобразования, trivia или
/// async-return semantics нельзя доказать, исходная форма сохраняется. Пользовательские
/// <see langword="finally"/> и финализаторы намеренно полностью исключены из traversal, чтобы
/// cancellation checkpoint не прервал cleanup. Повторный проход идемпотентен относительно уже
/// вставленных Stop-checkpoints.
/// </para>
/// </remarks>
internal sealed class CancellationInstrumentationRewriter : CSharpSyntaxRewriter
{
    // Fully-qualified checkpoint не зависит от using/alias пользователя и остаётся стабильной
    // публичной целью автоматически сгенерированного кода.
    private const string StopCheckCode =
        "global::KID.StopManager.StopIfButtonPressed();";

    // Ambient token текущей execution-сессии добавляется только в подтверждённые overload
    // Task.Delay, уже имеющие стандартный cancellation-aware эквивалент.
    private const string CurrentTokenCode =
        "global::KID.StopManager.CurrentToken";

    // StopManager.Sleep сохраняет поддержанные int/TimeSpan overload Thread.Sleep, но заменяет
    // непрерываемое ожидание token-aware WaitHandle ожиданием внутри KID.Library.
    private const string SleepTargetCode =
        "global::KID.StopManager.Sleep";

    // SemanticModel относится к исходному tree текущего pass. Token проверяется как самим
    // Visitor, так и Roslyn semantic lookup для потенциально переписываемых invocation nodes.
    private readonly SemanticModel semanticModel;
    private readonly CancellationToken cancellationToken;

    // Runtime-resolved symbols запрещают принимать source-defined тип с тем же metadata name
    // за System.Threading.Thread или System.Threading.Tasks.Task.
    private readonly INamedTypeSymbol? systemThreadType;
    private readonly INamedTypeSymbol? systemTaskType;

    /// <summary>
    /// Создаёт semantic-aware cancellation Visitor для одного конкретного syntax tree.
    /// </summary>
    /// <param name="semanticModel">
    /// Модель Roslyn, принадлежащая дереву, которое будет передано в <see cref="Visit"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен execution-сессии, позволяющий Stop отменить сам traversal и semantic analysis.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="semanticModel"/> имеет значение <see langword="null"/>.
    /// </exception>
    public CancellationInstrumentationRewriter(
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        /* SemanticModel является обязательной safety boundary для invocation rewrite.
         * Fail-fast не позволяет деградировать к опасному сравнению методов только по имени.
         */
        this.semanticModel = semanticModel ??
            throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;

        /* Symbols известных runtime types разрешаются один раз на весь Visitor pass. null
         * означает отсутствующую reference и приводит к безопасному отказу от соответствующего
         * invocation rewrite, не мешая структурному добавлению checkpoints.
         */
        systemThreadType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Thread));
        systemTaskType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Task));
    }

    /// <summary>
    /// Проверяет session token перед рекурсивным посещением каждого syntax node.
    /// </summary>
    /// <remarks>
    /// Override централизует cooperative cancellation всего traversal. Благодаря ему Stop
    /// наблюдается и в поддеревьях, для которых класс не объявляет специализированный Visit*-метод.
    /// </remarks>
    /// <param name="node">Текущий node либо <see langword="null"/> по контракту Roslyn.</param>
    /// <returns>Результат стандартного recursive rewrite для переданного node.</returns>
    /// <exception cref="OperationCanceledException">
    /// Для текущей execution-сессии запрошен Stop.
    /// </exception>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        /* Check выполняется до входа в очередное поддерево, поэтому после отмены Visitor не
         * продолжает создавать новые syntax nodes и разматывает recursion обычным OCE.
         */
        cancellationToken.ThrowIfCancellationRequested();
        return base.Visit(node);
    }

    /// <summary>
    /// Сохраняет пользовательский <see langword="finally"/> полностью неизменным и не посещает
    /// его дочерние nodes.
    /// </summary>
    /// <remarks>
    /// Finally является cleanup path. Checkpoint либо token-aware Sleep внутри него мог бы
    /// выбросить cancellation exception и прервать освобождение пользовательских ресурсов.
    /// </remarks>
    /// <param name="node">Исходный finally clause.</param>
    /// <returns>Тот же node без recursive traversal.</returns>
    public override SyntaxNode? VisitFinallyClause(FinallyClauseSyntax node) => node;

    /// <summary>
    /// Сохраняет финализатор полностью неизменным и не посещает его дочерние nodes.
    /// </summary>
    /// <remarks>
    /// Финализатор выполняется CLR как аварийный cleanup path. Он не должен зависеть от ambient
    /// execution token или завершаться раньше из-за автоматически вставленного Stop.
    /// </remarks>
    /// <param name="node">Исходное объявление destructor/finalizer.</param>
    /// <returns>Тот же node без recursive traversal.</returns>
    public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) => node;

    /// <summary>
    /// Инструментирует top-level program: добавляет checkpoint перед первым global statement
    /// и перед поддержанными statement-level await/yield continuation points.
    /// </summary>
    /// <param name="node">Корневой compilation unit пользовательской программы.</param>
    /// <returns>Новый root с инструментированными global statements.</returns>
    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        /* Сначала base Visitor переписывает содержимое global statements: циклы, вложенные
         * callable bodies и invocation expressions должны быть готовы до сборки нового списка.
         */
        var rewritten = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;

        /* Дополнительная ёмкость учитывает обычный единственный entry checkpoint. При наличии
         * await/yield список может вырасти больше; List самостоятельно расширится без влияния
         * на семантику результата.
         */
        var members = new List<MemberDeclarationSyntax>(rewritten.Members.Count + 1);
        var seenGlobalStatement = false;

        foreach (var member in rewritten.Members)
        {
            /* Type/namespace declarations и другие non-global members сохраняют исходный порядок.
             * Их внутренние callable bodies уже обработаны recursive base pass.
             */
            if (member is not GlobalStatementSyntax globalStatement)
            {
                members.Add(member);
                continue;
            }

            /* Первый global statement является неявным входом top-level Main. Checkpoint перед
             * ним даёт Stop-point даже программе без методов и циклов. IsStopCheck обеспечивает
             * идемпотентность повторного rewriter pass.
             */
            if (!seenGlobalStatement && !IsStopCheck(globalStatement.Statement))
                members.Add(SyntaxFactory.GlobalStatement(CreateStopCheck()));

            /* Statement-level await/yield получает checkpoint непосредственно перед statement.
             * LastOrDefault не допускает дубликат, если entry checkpoint уже занял ту же позицию.
             */
            if (NeedsContinuationCheck(globalStatement.Statement) &&
                (members.LastOrDefault() is not GlobalStatementSyntax previous ||
                 !IsStopCheck(previous.Statement)))
            {
                members.Add(SyntaxFactory.GlobalStatement(CreateStopCheck()));
            }

            /* Сам global statement добавляется после всех относящихся к нему checkpoints.
             * Флаг фиксирует, что последующие global statements уже не являются program entry.
             */
            members.Add(globalStatement);
            seenGlobalStatement = true;
        }

        /* Immutable root получает новый ordered member list; usings, attributes, EOF token и
         * прочие свойства CompilationUnit сохраняются экземпляром rewritten.
         */
        return rewritten.WithMembers(SyntaxFactory.List(members));
    }

    /// <summary>
    /// Добавляет checkpoints перед поддержанными await/yield statements внутри обычного блока.
    /// </summary>
    /// <param name="node">Исходный block-bodied список statements.</param>
    /// <returns>Переписанный блок с сохранёнными braces и новым списком statements.</returns>
    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        /* Descendants переписываются первыми; затем к готовому ordered list применяется
         * локальная вставка continuation checkpoints без повторного traversal новых nodes.
         */
        var rewritten = (BlockSyntax)base.VisitBlock(node)!;
        return rewritten.WithStatements(AddContinuationChecks(rewritten.Statements));
    }

    /// <summary>
    /// Добавляет checkpoints перед поддержанными await/yield statements внутри одной case-секции.
    /// </summary>
    /// <param name="node">Switch section с labels и ordered statements.</param>
    /// <returns>Переписанная секция с неизменными labels и инструментированными statements.</returns>
    public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
    {
        /* SwitchSection не является BlockSyntax, поэтому требует отдельного применения того же
         * statement-list algorithm после recursive rewrite descendants.
         */
        var rewritten = (SwitchSectionSyntax)base.VisitSwitchSection(node)!;
        return rewritten.WithStatements(AddContinuationChecks(rewritten.Statements));
    }

    /// <summary>
    /// Добавляет checkpoint первым действием каждой итерации <see langword="while"/>.
    /// </summary>
    /// <param name="node">Исходный while statement.</param>
    /// <returns>While statement с block-bodied инструментированным телом.</returns>
    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        /* Сначала переписываются condition и body descendants, затем body нормализуется:
         * существующий block получает entry check, embedded statement оборачивается в block.
         */
        var rewritten = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет checkpoint первым действием каждой итерации <see langword="do"/>/
    /// <see langword="while"/>.
    /// </summary>
    /// <param name="node">Исходный do statement.</param>
    /// <returns>Do statement с инструментированным телом и неизменным condition.</returns>
    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        /* Checkpoint располагается внутри body, поэтому выполняется до пользовательского кода
         * каждой итерации, включая обязательную первую итерацию do/while.
         */
        var rewritten = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет checkpoint первым действием каждой итерации <see langword="for"/>.
    /// </summary>
    /// <param name="node">Исходный for statement.</param>
    /// <returns>For statement с неизменными initializer/condition/incrementors и новым body.</returns>
    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        /* Checkpoint добавляется только в body: initializer выполняется один раз, incrementors
         * сохраняют исходный порядок, а Stop наблюдается перед пользовательским телом итерации.
         */
        var rewritten = (ForStatementSyntax)base.VisitForStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет checkpoint первым действием каждой итерации обычного
    /// <see langword="foreach"/> и <see langword="await foreach"/>.
    /// </summary>
    /// <param name="node">Исходный foreach statement.</param>
    /// <returns>Foreach statement с инструментированным телом.</returns>
    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        /* Await keyword и enumerator expression остаются частью rewritten node; transformation
         * затрагивает только тело уже полученной итерации.
         */
        var rewritten = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет checkpoint в тело foreach с variable designation/deconstruction.
    /// </summary>
    /// <param name="node">Исходный foreach-variable statement.</param>
    /// <returns>Foreach-variable statement с инструментированным телом.</returns>
    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        /* Отдельный Roslyn node kind требует отдельного override, хотя body policy совпадает
         * с обычным ForEachStatementSyntax.
         */
        var rewritten = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Оборачивает embedded ветви if/else, когда содержащийся await/yield требует checkpoint.
    /// </summary>
    /// <param name="node">Исходный if statement с опциональной else clause.</param>
    /// <returns>Переписанный if с instrumented embedded continuation statements.</returns>
    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        /* Base pass сначала обрабатывает condition, обе ветви и все вложенные конструкции.
         * Затем отдельно нормализуется else statement, если else присутствует.
         */
        var rewritten = (IfStatementSyntax)base.VisitIfStatement(node)!;
        var rewrittenElse = rewritten.Else is null
            ? null
            : rewritten.Else.WithStatement(
                AddEmbeddedContinuationCheckpoint(rewritten.Else.Statement));

        /* Обычная и else-ветви получают одинаковую policy. Уже block-bodied ветвь обработана
         * VisitBlock и не оборачивается повторно helper-методом.
         */
        return rewritten
            .WithStatement(AddEmbeddedContinuationCheckpoint(rewritten.Statement))
            .WithElse(rewrittenElse);
    }

    /// <summary>
    /// Оборачивает embedded body using statement, если оно само является await-containing
    /// statement без отдельного блока.
    /// </summary>
    /// <param name="node">Исходный using statement.</param>
    /// <returns>Using statement с при необходимости обёрнутым body.</returns>
    public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
    {
        /* Await using declaration распознаётся отдельно в NeedsContinuationCheck. Этот override
         * обслуживает embedded body формы using (...), которая может не иметь braces.
         */
        var rewritten = (UsingStatementSyntax)base.VisitUsingStatement(node)!;
        return rewritten.WithStatement(
            AddEmbeddedContinuationCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Оборачивает labeled embedded statement, если перед его await/yield требуется checkpoint.
    /// </summary>
    /// <param name="node">Исходный labeled statement.</param>
    /// <returns>Labeled statement с сохранённой label и при необходимости новым block body.</returns>
    public override SyntaxNode? VisitLabeledStatement(LabeledStatementSyntax node)
    {
        /* Checkpoint помещается внутрь labeled statement, поэтому goto продолжает переходить
         * к label и затем обязательно проходит через вставленную cancellation point.
         */
        var rewritten = (LabeledStatementSyntax)base.VisitLabeledStatement(node)!;
        return rewritten.WithStatement(
            AddEmbeddedContinuationCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет checkpoint на входе в block-bodied method либо безопасно преобразует
    /// поддержанный expression-bodied method в эквивалентный block.
    /// </summary>
    /// <remarks>
    /// Return/async semantics expression body вычисляется по symbol исходного объявления до
    /// recursive rewrite. Пользовательский task-like тип без доказуемого стандартного поведения
    /// остаётся в исходной expression-bodied форме.
    /// </remarks>
    /// <param name="node">Исходное объявление метода.</param>
    /// <returns>Метод с entry checkpoint либо неизменённая неподдержанная форма.</returns>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        /* SemanticModel знает symbol только исходного declaration node. Поэтому требуемое
         * поведение expression body фиксируется до вызова base, создающего новые syntax nodes.
         */
        var expressionBehavior = GetMethodExpressionBehavior(node);
        var rewritten = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

        /* Обычный block-bodied метод всегда получает checkpoint первым statement. Helper
         * распознаёт уже существующий сгенерированный check и сохраняет идемпотентность.
         */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Abstract/extern/partial signature не имеет body. Неподдержанный expression body также
         * сохраняется без рискованного изменения async/return semantics.
         */
        if (rewritten.ExpressionBody is null || expressionBehavior is null)
            return rewritten;

        /* Подтверждённая expression-bodied форма превращается в block из checkpoint и одного
         * return/expression/throw statement. Arrow и semicolon trivia переносятся helper-методом.
         */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                expressionBehavior.Value))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет entry checkpoint в local function по тем же правилам, что и в обычный метод.
    /// </summary>
    /// <param name="node">Исходное объявление local function.</param>
    /// <returns>Инструментированная либо безопасно сохранённая local function.</returns>
    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        /* Local function имеет собственный IMethodSymbol и отдельный execution entry, поэтому
         * checkpoint нужен при каждом её вызове, а не только на входе enclosing method.
         */
        var expressionBehavior = GetMethodExpressionBehavior(node);
        var rewritten = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;

        /* Block body получает тот же idempotent entry algorithm, что MethodDeclarationSyntax. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Signature-only либо custom task-like expression body остаётся неизменной. */
        if (rewritten.ExpressionBody is null || expressionBehavior is null)
            return rewritten;

        /* Поддержанный expression body преобразуется с заранее вычисленным поведением результата. */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                expressionBehavior.Value))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет checkpoint на входе в constructor и преобразует безопасный expression body.
    /// </summary>
    /// <param name="node">Исходное объявление instance или static constructor.</param>
    /// <returns>Constructor с entry checkpoint либо неизменённая неподдержанная форма.</returns>
    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        /* Возможность конвертации проверяется на исходном ArrowExpressionClause, пока trivia и
         * directives ещё принадлежат SemanticModel-associated tree.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;

        /* Constructor block получает Stop до первого пользовательского body statement.
         * Constructor initializer (: base/: this) остаётся вне body и не переставляется.
         */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Отсутствующее body или directives около arrow запрещают structural conversion. */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Constructor ничего не возвращает, поэтому expression body становится обычным
         * ExpressionStatement после checkpoint.
         */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Expression))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет checkpoint на входе в пользовательский operator и преобразует безопасный
    /// expression body в block с return.
    /// </summary>
    /// <param name="node">Исходное объявление operator.</param>
    /// <returns>Инструментированное объявление operator.</returns>
    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        /* Operator всегда имеет возвращаемое значение, поэтому поддержанный expression body
         * требует ReturnStatement; возможность конвертации зависит прежде всего от directives.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;

        /* Существующий block получает idempotent entry checkpoint. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Небезопасная trivia/directive форма сохраняется без изменения. */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Expression либо throw expression превращается helper-методом в return/throw statement. */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Return))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет checkpoint на входе в conversion operator и преобразует безопасный
    /// expression body в block с return.
    /// </summary>
    /// <param name="node">Исходное объявление implicit/explicit conversion operator.</param>
    /// <returns>Инструментированный conversion operator.</returns>
    public override SyntaxNode? VisitConversionOperatorDeclaration(
        ConversionOperatorDeclarationSyntax node)
    {
        /* Решение о structural conversion принимается по исходной arrow trivia до base rewrite. */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (ConversionOperatorDeclarationSyntax)
            base.VisitConversionOperatorDeclaration(node)!;

        /* Block-bodied conversion получает checkpoint до вычисления пользовательского результата. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Expression body с directives не перемещается в block без надёжного line mapping. */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Conversion operator возвращает значение целевого типа, поэтому используется Return. */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Return))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет checkpoint в get/set/init/add/remove accessor и безопасно преобразует его
    /// expression-bodied форму с учётом возвращаемого поведения.
    /// </summary>
    /// <param name="node">Исходное объявление accessor.</param>
    /// <returns>Инструментированный accessor либо исходная semicolon-only форма.</returns>
    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        /* Auto-property/event accessor без body останется без изменений. Для expression body
         * заранее проверяется отсутствие directives, которые нельзя безопасно переместить.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;

        /* Явный accessor block получает checkpoint перед первым пользовательским statement. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Semicolon-only либо directive-sensitive expression body сохраняется как есть. */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Getter возвращает значение и требует ReturnStatement. Setter/init/event accessors
         * имеют void-like behavior и используют ExpressionStatement.
         */
        var behavior = rewritten.Kind() == SyntaxKind.GetAccessorDeclaration
            ? ExpressionBodyBehavior.Return
            : ExpressionBodyBehavior.Expression;

        /* Arrow body заменяется block, а прежние ExpressionBody/Semicolon очищаются, чтобы
         * declaration не содержал две взаимоисключающие формы body.
         */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                behavior))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет checkpoint на входе в anonymous method <c>delegate { ... }</c>.
    /// </summary>
    /// <param name="node">Исходное anonymous method expression.</param>
    /// <returns>Anonymous method с инструментированным block.</returns>
    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        /* AnonymousMethodSyntax всегда имеет BlockSyntax, поэтому не требует анализа
         * expression-body return semantics.
         */
        var rewritten = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    /// <summary>
    /// Добавляет checkpoint в block-bodied parenthesized lambda; expression-bodied lambda
    /// намеренно оставляет без structural conversion.
    /// </summary>
    /// <param name="node">Lambda с parenthesized parameter list.</param>
    /// <returns>Lambda с инструментированным block либо исходной expression body.</returns>
    public override SyntaxNode? VisitParenthesizedLambdaExpression(
        ParenthesizedLambdaExpressionSyntax node)
    {
        /* Descendants expression-bodied lambda всё равно посещаются и могут получить безопасный
         * invocation rewrite, но сама expression не оборачивается без target delegate semantics.
         */
        var rewritten = (ParenthesizedLambdaExpressionSyntax)
            base.VisitParenthesizedLambdaExpression(node)!;

        /* Block null однозначно обозначает expression-bodied lambda. */
        return rewritten.Block is null
            ? rewritten
            : rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    /// <summary>
    /// Добавляет checkpoint в block-bodied simple lambda; expression-bodied lambda сохраняет
    /// свою форму и inferred return conversion.
    /// </summary>
    /// <param name="node">Lambda с одним непомещённым в скобки параметром.</param>
    /// <returns>Lambda с инструментированным block либо исходной expression body.</returns>
    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        /* Для expression-bodied lambda тип результата определяется target delegate/expression tree.
         * Без отдельного semantic proof преобразование в block могло бы изменить overload binding.
         */
        var rewritten = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
        return rewritten.Block is null
            ? rewritten
            : rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    /// <summary>
    /// Переписывает только семантически подтверждённые поддержанные вызовы
    /// <see cref="Thread.Sleep(int)"/> и одноаргументные <see cref="Task.Delay(int)"/>.
    /// </summary>
    /// <remarks>
    /// Sleep получает target <see cref="global::KID.StopManager.Sleep(int)"/>, а Delay — именованный
    /// аргумент <c>cancellationToken: StopManager.CurrentToken</c>. Остальные invocation expressions
    /// проходят только стандартный recursive rewrite descendants.
    /// </remarks>
    /// <param name="node">Исходный invocation expression.</param>
    /// <returns>Cancellation-aware invocation либо исходная семантическая форма.</returns>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        /* Symbol запрашивается у исходного node до base traversal: SemanticModel не обслуживает
         * новые nodes, которые Visitor создаст для вложенных expressions.
         */
        var method = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;

        /* Сначала сохраняются все изменения descendants. Outer rewrite затем меняет только target
         * либо argument list уже переписанного invocation.
         */
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        /* Thread.Sleep заменяется только при точной BCL identity и безопасной trivia target.
         * Ограничение trivia предотвращает потерю комментариев, line breaks и directives внутри
         * сложного member-access expression.
         */
        if (IsThreadSleep(method) && CanReplaceInvocationTarget(node.Expression))
        {
            /* Fully-qualified target сохраняет overload resolution по существующему argument,
             * а WithTriviaFrom переносит внешнюю trivia исходного invocation target.
             */
            var target = SyntaxFactory.ParseExpression(SleepTargetCode)
                .WithTriviaFrom(rewritten.Expression);
            return rewritten.WithExpression(target);
        }

        /* Только одноаргументный BCL Task.Delay имеет однозначный cancellation-aware overload,
         * получаемый добавлением второго именованного argument.
         */
        if (IsSingleArgumentTaskDelay(method))
        {
            /* NameColon делает намерение явным и не зависит от позиционного порядка будущих
             * overload forms. Ambient token берётся в момент выполнения пользовательского кода.
             */
            var tokenArgument = SyntaxFactory.Argument(
                    SyntaxFactory.ParseExpression(CurrentTokenCode))
                .WithNameColon(SyntaxFactory.NameColon("cancellationToken"));

            /* Исходный argument и его trivia сохраняются, новый token добавляется последним. */
            return rewritten.WithArgumentList(
                rewritten.ArgumentList.WithArguments(
                    rewritten.ArgumentList.Arguments.Add(tokenArgument)));
        }

        /* Неразрешённый symbol, другой overload или пользовательский API остаются unchanged. */
        return rewritten;
    }

    /// <summary>
    /// Определяет, каким statement должен стать expression body метода или local function после
    /// добавления entry checkpoint.
    /// </summary>
    /// <param name="node">
    /// Исходный <see cref="MethodDeclarationSyntax"/> или
    /// <see cref="LocalFunctionStatementSyntax"/> из связанной semantic model.
    /// </param>
    /// <returns>
    /// <see cref="ExpressionBodyBehavior.Expression"/> для void-подобного результата,
    /// <see cref="ExpressionBodyBehavior.Return"/> для возвращаемого значения либо
    /// <see langword="null"/>, если безопасное преобразование не доказано.
    /// </returns>
    private ExpressionBodyBehavior? GetMethodExpressionBehavior(SyntaxNode node)
    {
        /* Helper имеет закрытый допустимый набор declaration kinds. Любой иной node не содержит
         * ожидаемого expression body и приводит к fail-closed null через switch default.
         */
        var expressionBody = node switch
        {
            MethodDeclarationSyntax method => method.ExpressionBody,
            LocalFunctionStatementSyntax localFunction => localFunction.ExpressionBody,
            _ => null
        };

        /* Directives около arrow/body нельзя перемещать в braces без доказанного line mapping.
         * Отсутствующий expression body также означает, что helper не должен выбирать behavior.
         */
        if (!CanConvertExpressionBody(expressionBody))
            return null;

        /* Declared symbol запрашивается у исходного declaration node. Если Roslyn не может
         * однозначно предоставить IMethodSymbol, textual return type не используется как fallback.
         */
        var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken) as IMethodSymbol;
        if (symbol is null)
            return null;

        /* void expression body должен остаться ExpressionStatement: добавление return было бы
         * синтаксически и семантически неверным.
         */
        if (symbol.ReturnsVoid)
            return ExpressionBodyBehavior.Expression;

        /* Обычный синхронный non-void method возвращает значение выражения напрямую. */
        if (!symbol.IsAsync)
            return ExpressionBodyBehavior.Return;

        /* В async Task/ValueTask без результата исходное expression body представляет await-like
         * side effect, поэтому block использует ExpressionStatement без return.
         */
        if (IsKnownTaskLike(symbol.ReturnType, arity: 0))
            return ExpressionBodyBehavior.Expression;

        /* В async Task<T>/ValueTask<T> expression вычисляет логический T-result и должен стать
         * ReturnStatement внутри async method body.
         */
        if (IsKnownTaskLike(symbol.ReturnType, arity: 1))
            return ExpressionBodyBehavior.Return;

        /* Пользовательские task-like типы зависят от AsyncMethodBuilder и могут иметь отличную
         * conversion semantics. Без отдельного semantic proof они намеренно не переписываются.
         */
        return null;
    }

    /// <summary>
    /// Проверяет стандартную Task/ValueTask форму по namespace, имени и generic arity.
    /// </summary>
    /// <param name="returnType">Roslyn return type symbol анализируемого async method.</param>
    /// <param name="arity">Ожидаемое число generic type arguments: 0 либо 1.</param>
    /// <returns>
    /// <see langword="true"/> для стандартного Task/ValueTask shape; иначе
    /// <see langword="false"/>.
    /// </returns>
    private static bool IsKnownTaskLike(ITypeSymbol returnType, int arity) =>
        /* Проверка intentionally структурная и узкая. Она не пытается интерпретировать custom
         * awaitables либо типы Task из другого namespace как стандартный async contract.
         */
        returnType is INamedTypeSymbol namedType &&
        namedType.Arity == arity &&
        namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        namedType.Name is "Task" or "ValueTask";

    /// <summary>
    /// Проверяет, можно ли перенести expression body в block без перемещения directives.
    /// </summary>
    /// <param name="expressionBody">Arrow expression clause либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/>, если expression body существует и его arrow/body не содержит
    /// preprocessor directives; иначе <see langword="false"/>.
    /// </returns>
    private static bool CanConvertExpressionBody(ArrowExpressionClauseSyntax? expressionBody) =>
        /* Directives проверяются как во всём clause, так и в leading/trailing trivia arrow token.
         * Некоторая из проверок может пересекаться, но явное покрытие границ делает policy
         * устойчивой к разным trivia layouts Roslyn.
         */
        expressionBody is not null &&
        !expressionBody.ContainsDirectives &&
        !expressionBody.ArrowToken.LeadingTrivia.Any(IsDirectiveTrivia) &&
        !expressionBody.ArrowToken.TrailingTrivia.Any(IsDirectiveTrivia);

    /// <summary>
    /// Определяет, представляет ли trivia структурированный preprocessor directive.
    /// </summary>
    /// <param name="trivia">Проверяемая trivia arrow token.</param>
    /// <returns><see langword="true"/> только для <see cref="DirectiveTriviaSyntax"/>.</returns>
    private static bool IsDirectiveTrivia(SyntaxTrivia trivia) =>
        /* HasStructure избегает ненужного GetStructure для обычных spaces/comments. */
        trivia.HasStructure && trivia.GetStructure() is DirectiveTriviaSyntax;

    /// <summary>
    /// Строит block-bodied эквивалент expression body из entry checkpoint и одного
    /// expression/return/throw statement с сохранением значимой trivia.
    /// </summary>
    /// <param name="expressionBody">Подтверждённый безопасный arrow expression clause.</param>
    /// <param name="semicolonToken">Исходный завершающий semicolon declaration.</param>
    /// <param name="behavior">
    /// Требование представить обычное expression как ExpressionStatement или ReturnStatement.
    /// </param>
    /// <returns>Новый block, заменяющий expression body declaration.</returns>
    private static BlockSyntax CreateExpressionBody(
        ArrowExpressionClauseSyntax expressionBody,
        SyntaxToken semicolonToken,
        ExpressionBodyBehavior behavior)
    {
        /* Trailing trivia исходного semicolon должна принадлежать закрывающей brace всего block,
         * а не внутреннему statement. Поэтому для statement создаётся token без trailing trivia.
         */
        var statementSemicolon = semicolonToken.WithTrailingTrivia();

        /* Throw expression во всех поддержанных declaration forms превращается в ThrowStatement.
         * Остальные expressions следуют заранее доказанному return behavior.
         */
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

        /* Leading/trailing trivia arrow token переносится на opening brace. Это сохраняет
         * whitespace/comments вокруг прежнего => без добавления новых source lines.
         */
        var openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
            .WithLeadingTrivia(expressionBody.ArrowToken.LeadingTrivia)
            .WithTrailingTrivia(expressionBody.ArrowToken.TrailingTrivia);

        /* Block содержит ровно два statements: стабильный Stop checkpoint и исходное вычисление.
         * Trailing trivia прежнего semicolon переносится на closing brace declaration.
         */
        return SyntaxFactory.Block(CreateStopCheck(), expressionStatement)
            .WithOpenBraceToken(openBrace)
            .WithCloseBraceToken(
                SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithTrailingTrivia(semicolonToken.TrailingTrivia));
    }

    /// <summary>
    /// Вставляет Stop checkpoint первым statement block, если он ещё не присутствует.
    /// </summary>
    /// <param name="block">Block callable или loop body.</param>
    /// <returns>Исходный idempotent block либо block с добавленным первым statement.</returns>
    private static BlockSyntax AddEntryCheckpoint(BlockSyntax block)
    {
        /* Structural equivalence с canonical checkpoint предотвращает повторную вставку при
         * повторном запуске rewriter над уже инструментированным source.
         */
        if (block.Statements.FirstOrDefault() is { } firstStatement &&
            IsStopCheck(firstStatement))
        {
            return block;
        }

        /* Insert сохраняет относительный порядок всех пользовательских statements и braces block. */
        return block.WithStatements(block.Statements.Insert(0, CreateStopCheck()));
    }

    /// <summary>
    /// Гарантирует checkpoint в начале loop body, сохраняя block либо оборачивая одиночный
    /// embedded statement в новый block.
    /// </summary>
    /// <param name="statement">Переписанное тело while/do/for/foreach.</param>
    /// <returns>Block-bodied или уже canonical checkpoint statement.</returns>
    private static StatementSyntax AddLoopCheckpoint(StatementSyntax statement)
    {
        /* Существующий block использует общий entry algorithm и не получает лишнего уровня braces. */
        if (statement is BlockSyntax block)
            return AddEntryCheckpoint(block);

        /* Защитная idempotency ветвь сохраняет standalone canonical check без обёртки. */
        if (IsStopCheck(statement))
            return statement;

        /* Embedded statement оборачивается в block, где checkpoint всегда выполняется первым,
         * а исходный statement остаётся вторым без изменения своей внутренней структуры.
         */
        return SyntaxFactory.Block(CreateStopCheck(), statement);
    }

    /// <summary>
    /// Добавляет checkpoint к embedded statement с await/yield, если оно не имеет собственного
    /// block и действительно является continuation candidate.
    /// </summary>
    /// <param name="statement">Statement ветви if/else, using либо label.</param>
    /// <returns>Исходный statement либо новый block из checkpoint и statement.</returns>
    private static StatementSyntax AddEmbeddedContinuationCheckpoint(
        StatementSyntax statement)
    {
        /* Block уже обработан VisitBlock. Statement без await/yield не должен получать лишние
         * braces и cancellation points за пределами согласованного instrumentation scope.
         */
        if (statement is BlockSyntax || !NeedsContinuationCheck(statement))
            return statement;

        /* Обёртка сохраняет embedded statement как единое тело parent construct. */
        return SyntaxFactory.Block(CreateStopCheck(), statement);
    }

    /// <summary>
    /// Вставляет checkpoint непосредственно перед каждым поддержанным await/yield statement
    /// в ordered list, не создавая соседние дубликаты.
    /// </summary>
    /// <param name="statements">Statements обычного block или switch section.</param>
    /// <returns>Новый immutable statement list с сохранённым пользовательским порядком.</returns>
    private static SyntaxList<StatementSyntax> AddContinuationChecks(
        SyntaxList<StatementSyntax> statements)
    {
        /* Mutable builder упрощает условную вставку нескольких элементов, после чего результат
         * снова превращается в Roslyn immutable SyntaxList.
         */
        var rewritten = new List<StatementSyntax>(statements.Count);

        foreach (var statement in statements)
        {
            /* Checkpoint добавляется только для candidate и только если предыдущим элементом ещё
             * не является canonical check. Это объединяет method-entry/loop-entry check с первым
             * await statement и обеспечивает идемпотентность повторного pass.
             */
            if (NeedsContinuationCheck(statement) &&
                (rewritten.LastOrDefault() is not { } previous || !IsStopCheck(previous)))
            {
                rewritten.Add(CreateStopCheck());
            }

            /* Пользовательский statement всегда добавляется ровно один раз и после своего check. */
            rewritten.Add(statement);
        }

        /* SyntaxFactory.List фиксирует окончательный ordered immutable snapshot. */
        return SyntaxFactory.List(rewritten);
    }

    /// <summary>
    /// Определяет, является ли statement безопасно поддержанным await/yield continuation candidate.
    /// </summary>
    /// <remarks>
    /// Поиск await не спускается во вложенные lambda и local functions: у них отдельный execution
    /// entry и собственная instrumentation policy. Произвольные compound statements анализируются
    /// специализированными Visit*-методами, чтобы checkpoint оказался в правильной ветви/body.
    /// </remarks>
    /// <param name="statement">Проверяемый statement после recursive rewrite descendants.</param>
    /// <returns><see langword="true"/>, если непосредственно перед statement нужен checkpoint.</returns>
    private static bool NeedsContinuationCheck(StatementSyntax statement)
    {
        /* Любой yield return/yield break является явной iterator suspension boundary. */
        if (statement is YieldStatementSyntax)
            return true;

        /* await using declaration представлен LocalDeclarationStatementSyntax с AwaitKeyword,
         * даже если descendant AwaitExpressionSyntax отсутствует.
         */
        if (statement is LocalDeclarationStatementSyntax localDeclaration &&
            localDeclaration.AwaitKeyword != default)
        {
            return true;
        }

        /* Statement-form await using хранит AwaitKeyword непосредственно в UsingStatementSyntax. */
        if (statement is UsingStatementSyntax usingStatement &&
            usingStatement.AwaitKeyword != default)
        {
            return true;
        }

        /* Ограниченный whitelist statement shapes исключает if/loops/try и другие compound nodes:
         * их корректная позиция checkpoint определяется соответствующим Visit override.
         */
        if (statement is not ExpressionStatementSyntax and
            not LocalDeclarationStatementSyntax and
            not ReturnStatementSyntax and
            not ThrowStatementSyntax)
        {
            return false;
        }

        /* Для простых statement shapes ищется любой AwaitExpression, но traversal прекращается
         * перед nested executable scopes, которые не выполняются непосредственно этим statement.
         */
        return statement.DescendantNodes(
                ShouldDescendIntoContinuationCandidate)
            .OfType<AwaitExpressionSyntax>()
            .Any();
    }

    /// <summary>
    /// Ограничивает descendant traversal continuation candidate текущим executable scope.
    /// </summary>
    /// <param name="node">Потенциальный descendant проверяемого statement.</param>
    /// <returns>
    /// <see langword="false"/> для nested lambda/local function boundaries; иначе
    /// <see langword="true"/>.
    /// </returns>
    private static bool ShouldDescendIntoContinuationCandidate(SyntaxNode node) =>
        /* Await внутри lambda/local function выполняется только при отдельном будущем вызове и
         * не должен заставлять enclosing declaration statement получать continuation check.
         */
        node is not AnonymousFunctionExpressionSyntax and
        not LocalFunctionStatementSyntax;

    /// <summary>
    /// Сравнивает statement с canonical автоматически генерируемым Stop checkpoint.
    /// </summary>
    /// <param name="statement">Проверяемый syntax statement.</param>
    /// <returns><see langword="true"/> при structural equivalence без учёта top-level режима.</returns>
    private static bool IsStopCheck(StatementSyntax statement) =>
        /* IsEquivalentTo сравнивает syntax structure, а не object identity. Поэтому check,
         * восстановленный parsing повторного pass, распознаётся так же, как только что созданный.
         */
        statement.IsEquivalentTo(CreateStopCheck(), topLevel: false);

    /// <summary>
    /// Создаёт canonical fully-qualified statement проверки Stop.
    /// </summary>
    /// <returns>Новый expression statement без пользовательской trivia.</returns>
    private static ExpressionStatementSyntax CreateStopCheck() =>
        /* ParseStatement централизует точный generated-code shape, используемый одновременно
         * для вставки и idempotency comparison.
         */
        (ExpressionStatementSyntax)SyntaxFactory.ParseStatement(StopCheckCode);

    /// <summary>
    /// Проверяет, можно ли заменить invocation target Thread.Sleep без потери внутренней trivia.
    /// </summary>
    /// <param name="expression">Исходный target invocation до recursive rewrite.</param>
    /// <returns>
    /// <see langword="true"/>, если target не содержит line breaks, directives или comments;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool CanReplaceInvocationTarget(ExpressionSyntax expression) =>
        /* Полная замена target одним parsed expression не имеет точного места для trivia внутри
         * member-access chain. Fail-closed policy оставляет такие редкие formatting-sensitive
         * формы неизменными вместо молчаливой потери комментария или directive.
         */
        !expression.DescendantTrivia(descendIntoTrivia: true)
            .Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia) ||
                           trivia.IsDirective ||
                           trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

    /// <summary>
    /// Проверяет supported overload настоящего BCL Thread.Sleep.
    /// </summary>
    /// <param name="method">Разрешённый invocation method symbol либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> для static Sleep(int/TimeSpan) runtime-типа Thread; иначе
    /// <see langword="false"/>.
    /// </returns>
    private bool IsThreadSleep(IMethodSymbol? method) =>
        /* Shape check отсеивает overloads до более дорогого symbol identity comparison.
         * Supported parameter ограничивает rewrite формами, точно представленными StopManager.
         */
        method is
        {
            Name: "Sleep",
            IsStatic: true,
            Parameters.Length: 1
        } &&
        systemThreadType is not null &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, systemThreadType) &&
        IsSupportedDelayParameter(method.Parameters[0].Type);

    /// <summary>
    /// Проверяет одноаргументный supported overload настоящего BCL Task.Delay.
    /// </summary>
    /// <param name="method">Разрешённый invocation method symbol либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> для static Delay(int/TimeSpan) runtime-типа Task; иначе
    /// <see langword="false"/>.
    /// </returns>
    private bool IsSingleArgumentTaskDelay(IMethodSymbol? method) =>
        /* Уже cancellation-aware двухаргументный overload намеренно не изменяется. Один argument
         * гарантирует, что добавление именованного cancellationToken приводит к известной BCL форме.
         */
        method is
        {
            Name: "Delay",
            IsStatic: true,
            Parameters.Length: 1
        } &&
        systemTaskType is not null &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, systemTaskType) &&
        IsSupportedDelayParameter(method.Parameters[0].Type);

    /// <summary>
    /// Проверяет параметр времени, общий для поддержанных Thread.Sleep и Task.Delay overloads.
    /// </summary>
    /// <param name="type">Roslyn type symbol единственного argument parameter.</param>
    /// <returns><see langword="true"/> только для <see cref="int"/> или <see cref="TimeSpan"/>.</returns>
    private static bool IsSupportedDelayParameter(ITypeSymbol type) =>
        /* Int32 надёжно определяется SpecialType, а TimeSpan сравнивается в fully-qualified форме,
         * исключающей влияние using aliases и одноимённых пользовательских namespaces.
         */
        type.SpecialType == SpecialType.System_Int32 ||
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
        "global::System.TimeSpan";

    /// <summary>
    /// Описывает statement semantics исходного expression body после преобразования в block.
    /// </summary>
    private enum ExpressionBodyBehavior
    {
        /// <summary>
        /// Выражение выполняется как side effect без возвращаемого statement-result.
        /// </summary>
        Expression,

        /// <summary>
        /// Значение выражения возвращается из method/operator/getter через ReturnStatement.
        /// </summary>
        Return
    }
}

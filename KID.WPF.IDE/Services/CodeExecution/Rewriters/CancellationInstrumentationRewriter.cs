using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Добавляет в пользовательский код точки кооперативной проверки Stop,
/// не изменяя публичные API KID. Семантические преобразования выполняются только для разрешённых
/// и однозначно определённых символов BCL.
/// </summary>
/// <remarks>
/// <para>
/// Класс реализует паттерн <b>Visitor (Посетитель)</b> и роль <b>Rewriter (Преобразователь)</b>
/// синтаксического дерева на базе <see cref="CSharpSyntaxRewriter"/> и является первым этапом
/// преобразования компилятора KID. Он создаёт новое неизменяемое дерево, в котором учебная
/// программа периодически вызывает <see cref="global::KID.StopManager.StopIfButtonPressed"/>.
/// </para>
/// <para>
/// Контрольные точки добавляются на входе в поддержанные циклы и вызываемые конструкции,
/// а также перед операторами с <see langword="await"/> и <see langword="yield"/>. Это не принудительное
/// завершение потока, а <b>Cooperative Cancellation (Кооперативная отмена)</b>: выполнение
/// останавливается только после достижения одной из вставленных или явно поддержанных точек
/// наблюдения токена.
/// </para>
/// <para>
/// Преобразования <see cref="Thread.Sleep(int)"/> и <see cref="Task.Delay(int)"/> дополнительно
/// используют <see cref="SemanticModel"/> и проверяют сборку целевого типа. Поэтому
/// пользовательские методы <c>Sleep</c>/<c>Delay</c>, одноимённые типы и собственные ожидаемые
/// объекты не преобразуются только из-за текстового сходства с BCL.
/// </para>
/// <para>
/// Преобразователь следует правилу <b>Fail Closed (Безопасный отказ)</b>: если нельзя доказать,
/// что изменение сохранит синтаксис и семантику программы, исходная форма остаётся нетронутой.
/// Блоки <see langword="finally"/> и финализаторы полностью исключены из обхода, чтобы проверка
/// Stop не прервала очистку ресурсов. Повторный проход не дублирует уже вставленные проверки.
/// </para>
/// </remarks>
internal sealed class CancellationInstrumentationRewriter : CSharpSyntaxRewriter
{
    // Полностью квалифицированная контрольная точка не зависит от пользовательских using
    // и псевдонимов.
    private const string StopCheckCode =
        "global::KID.StopManager.StopIfButtonPressed();";

    // Токен текущей сессии добавляется только в подтверждённые перегрузки Task.Delay,
    // уже имеющие стандартный вариант с CancellationToken.
    private const string CurrentTokenCode =
        "global::KID.StopManager.CurrentToken";

    // StopManager.Sleep сохраняет поддержанные перегрузки Thread.Sleep с int/TimeSpan, но заменяет
    // непрерываемое ожидание на WaitHandle, учитывающий токен, внутри KID.Library.
    private const string SleepTargetCode =
        "global::KID.StopManager.Sleep";

    // SemanticModel относится к исходному дереву текущего прохода. Токен проверяется как самим
    // посетителем, так и Roslyn при разрешении потенциально преобразуемых вызовов.
    private readonly SemanticModel semanticModel;
    private readonly CancellationToken cancellationToken;

    // Символы типов среды выполнения не позволяют принять объявленный в пользовательском коде
    // тип с тем же именем
    // за System.Threading.Thread или System.Threading.Tasks.Task.
    private readonly INamedTypeSymbol? systemThreadType;
    private readonly INamedTypeSymbol? systemTaskType;

    /// <summary>
    /// Создаёт семантический посетитель, добавляющий поддержку Stop в одно синтаксическое дерево.
    /// </summary>
    /// <param name="semanticModel">
    /// Модель Roslyn, принадлежащая дереву, которое будет передано в <see cref="Visit"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен сессии выполнения, позволяющий Stop отменить обход и семантический анализ.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="semanticModel"/> имеет значение <see langword="null"/>.
    /// </exception>
    public CancellationInstrumentationRewriter(
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        /* SemanticModel необходима для безопасного преобразования вызовов. Немедленный отказ
         * не позволяет незаметно перейти к опасному сравнению методов только по имени.
         */
        this.semanticModel = semanticModel ??
            throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;

        /* Символы известных типов разрешаются один раз на весь проход. null означает, что нужной
         * ссылки нет: соответствующий вызов останется без изменений, но структурные контрольные
         * точки по-прежнему будут добавлены.
         */
        systemThreadType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Thread));
        systemTaskType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Task));
    }

    /// <summary>
    /// Проверяет токен сессии перед рекурсивным посещением каждого узла синтаксиса.
    /// </summary>
    /// <remarks>
    /// Переопределение централизует кооперативную отмену всего обхода. Благодаря ему Stop
    /// наблюдается и в поддеревьях, для которых класс не объявляет специализированный Visit*-метод.
    /// </remarks>
    /// <param name="node">Текущий узел либо <see langword="null"/> по контракту Roslyn.</param>
    /// <returns>Результат стандартного рекурсивного преобразования для переданного узла.</returns>
    /// <exception cref="OperationCanceledException">
    /// Для текущей сессии запрошен Stop.
    /// </exception>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        /* Проверка выполняется до входа в очередное поддерево, поэтому после отмены посетитель
         * не продолжает создавать новые узлы и разматывает рекурсию через OperationCanceledException.
         */
        cancellationToken.ThrowIfCancellationRequested();
        return base.Visit(node);
    }

    /// <summary>
    /// Сохраняет пользовательский <see langword="finally"/> полностью неизменным и не посещает
    /// его дочерние узлы.
    /// </summary>
    /// <remarks>
    /// Finally отвечает за очистку ресурсов. Контрольная точка или Sleep с поддержкой токена
    /// внутри него могли бы выбросить исключение отмены
    /// и прервать освобождение пользовательских ресурсов.
    /// </remarks>
    /// <param name="node">Исходная секция finally.</param>
    /// <returns>Тот же узел без рекурсивного обхода.</returns>
    public override SyntaxNode? VisitFinallyClause(FinallyClauseSyntax node) => node;

    /// <summary>
    /// Сохраняет финализатор полностью неизменным и не посещает его дочерние узлы.
    /// </summary>
    /// <remarks>
    /// Финализатор выполняется CLR как аварийный путь очистки ресурсов. Он не должен зависеть
    /// от токена текущей сессии или завершаться раньше из-за автоматически вставленного Stop.
    /// </remarks>
    /// <param name="node">Исходное объявление деструктора/финализатора.</param>
    /// <returns>Тот же узел без рекурсивного обхода.</returns>
    public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) => node;

    /// <summary>
    /// Инструментирует программу верхнего уровня: добавляет контрольную точку перед первым
    /// глобальным оператором и перед поддержанными операторами с await/yield.
    /// </summary>
    /// <param name="node">Корневой узел пользовательской программы.</param>
    /// <returns>Новый корневой узел с инструментированными глобальными операторами.</returns>
    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        /* Сначала базовый посетитель преобразует содержимое глобальных операторов:
         * циклы, вложенные тела вызываемых конструкций и выражения вызова должны быть готовы
         * до сборки нового списка.
         */
        var rewritten = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;

        /* Дополнительная ёмкость учитывает обычную единственную входную контрольную точку. При наличии
         * await/yield список может вырасти больше; List самостоятельно расширится без влияния
         * на семантику результата.
         */
        var members = new List<MemberDeclarationSyntax>(rewritten.Members.Count + 1);
        var seenGlobalStatement = false;

        foreach (var member in rewritten.Members)
        {
            /* Объявления типов, пространств имён и другие неглобальные члены
             * сохраняют исходный порядок. Их внутренние тела вызываемых конструкций уже обработаны
             * рекурсивным базовым проходом.
             */
            if (member is not GlobalStatementSyntax globalStatement)
            {
                members.Add(member);
                continue;
            }

            /* Первый глобальный оператор является неявным входом в Main верхнего уровня.
             * Проверка перед ним позволяет остановить даже программу без методов и циклов.
             * IsStopCheck не допускает дубликат при повторном проходе преобразователя.
             */
            if (!seenGlobalStatement && !IsStopCheck(globalStatement.Statement))
                members.Add(SyntaxFactory.GlobalStatement(CreateStopCheck()));

            /* Await/yield уровня оператора получает контрольную точку непосредственно перед оператором.
             * LastOrDefault не допускает дубликат, если входная контрольная точка уже заняла
             * ту же позицию.
             */
            if (NeedsContinuationCheck(globalStatement.Statement) &&
                (members.LastOrDefault() is not GlobalStatementSyntax previous ||
                 !IsStopCheck(previous.Statement)))
            {
                members.Add(SyntaxFactory.GlobalStatement(CreateStopCheck()));
            }

            /* Сам глобальный оператор добавляется после всех относящихся к нему контрольных точек.
             * Флаг фиксирует, что последующие глобальные операторы уже не являются входом
             * в программу.
             */
            members.Add(globalStatement);
            seenGlobalStatement = true;
        }

        /* Неизменяемый корневой узел получает новый упорядоченный список членов; директивы using,
         * атрибуты, токен конца файла и прочие свойства
         * CompilationUnit сохраняются экземпляром rewritten.
         */
        return rewritten.WithMembers(SyntaxFactory.List(members));
    }

    /// <summary>
    /// Добавляет контрольные точки перед поддержанными операторами await/yield внутри обычного блока.
    /// </summary>
    /// <param name="node">Исходный блок операторов.</param>
    /// <returns>Переписанный блок с сохранёнными фигурными скобками и новым списком операторов.</returns>
    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        /* Дочерние узлы преобразуются первыми; затем в готовый упорядоченный список локально
         * добавляются контрольные точки продолжения
         * без повторного обхода новых узлов.
         */
        var rewritten = (BlockSyntax)base.VisitBlock(node)!;
        return rewritten.WithStatements(AddContinuationChecks(rewritten.Statements));
    }

    /// <summary>
    /// Добавляет контрольные точки перед поддержанными операторами await/yield внутри одной
    /// секции case.
    /// </summary>
    /// <param name="node">Секция switch с метками и упорядоченными операторами.</param>
    /// <returns>Переписанная секция с неизменными метками и инструментированными операторами.</returns>
    public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
    {
        /* SwitchSection не является BlockSyntax, поэтому требует отдельного применения того же
         * алгоритма списка операторов после рекурсивного преобразования
         * потомков.
         */
        var rewritten = (SwitchSectionSyntax)base.VisitSwitchSection(node)!;
        return rewritten.WithStatements(AddContinuationChecks(rewritten.Statements));
    }

    /// <summary>
    /// Добавляет контрольную точку первым действием каждой итерации <see langword="while"/>.
    /// </summary>
    /// <param name="node">Исходный оператор while.</param>
    /// <returns>Оператор while с инструментированным телом-блоком.</returns>
    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        /* Сначала преобразуются условие и дочерние узлы тела, затем тело нормализуется:
         * существующий блок получает входную проверку, а вложенный оператор
         * оборачивается в блок.
         */
        var rewritten = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет контрольную точку первым действием каждой итерации <see langword="do"/>/
    /// <see langword="while"/>.
    /// </summary>
    /// <param name="node">Исходный оператор do.</param>
    /// <returns>Оператор do с инструментированным телом и неизменным условием.</returns>
    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        /* Контрольная точка располагается внутри тела, поэтому выполняется до пользовательского кода
         * каждой итерации, включая обязательную первую итерацию do/while.
         */
        var rewritten = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет контрольную точку первым действием каждой итерации <see langword="for"/>.
    /// </summary>
    /// <param name="node">Исходный оператор for.</param>
    /// <returns>
    /// Оператор for с неизменными инициализатором, условием и выражениями приращения
    /// и новым телом.
    /// </returns>
    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        /* Контрольная точка добавляется только в тело: инициализатор выполняется один раз,
         * выражения приращения сохраняют исходный порядок, а Stop проверяется перед
         * пользовательским телом итерации.
         */
        var rewritten = (ForStatementSyntax)base.VisitForStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет контрольную точку первым действием каждой итерации обычного
    /// <see langword="foreach"/> и <see langword="await foreach"/>.
    /// </summary>
    /// <param name="node">Исходный оператор foreach.</param>
    /// <returns>Оператор foreach с инструментированным телом.</returns>
    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        /* Ключевое слово await и выражение перечислителя остаются частью
         * переписанного узла; преобразование затрагивает только тело уже полученной итерации.
         */
        var rewritten = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет контрольную точку в тело foreach с обозначением переменной или деконструкцией.
    /// </summary>
    /// <param name="node">Исходный оператор foreach-variable.</param>
    /// <returns>Оператор foreach-variable с инструментированным телом.</returns>
    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        /* Отдельный вид узла Roslyn требует отдельного переопределения, хотя правило обработки тела совпадает
         * с обычным ForEachStatementSyntax.
         */
        var rewritten = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return rewritten.WithStatement(AddLoopCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Оборачивает вложенные ветви if/else, когда содержащийся await/yield требует
    /// контрольной точки.
    /// </summary>
    /// <param name="node">Исходный оператор if с необязательной секцией else.</param>
    /// <returns>
    /// Переписанный if с инструментированными вложенными операторами продолжения.
    /// </returns>
    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        /* Базовый проход сначала обрабатывает условие, обе ветви и все вложенные конструкции.
         * Затем отдельно нормализуется оператор else, если else присутствует.
         */
        var rewritten = (IfStatementSyntax)base.VisitIfStatement(node)!;
        var rewrittenElse = rewritten.Else is null
            ? null
            : rewritten.Else.WithStatement(
                AddEmbeddedContinuationCheckpoint(rewritten.Else.Statement));

        /* Обычная и else-ветви получают одинаковое правило. Ветвь с телом-блоком уже обработана
         * VisitBlock и не оборачивается повторно.
         */
        return rewritten
            .WithStatement(AddEmbeddedContinuationCheckpoint(rewritten.Statement))
            .WithElse(rewrittenElse);
    }

    /// <summary>
    /// Оборачивает вложенное тело оператора using, если оно само является содержащим await
    /// оператором без отдельного блока.
    /// </summary>
    /// <param name="node">Исходный оператор using.</param>
    /// <returns>Оператор using с при необходимости обёрнутым телом.</returns>
    public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
    {
        /* Объявление await using распознаётся отдельно в NeedsContinuationCheck. Это
         * переопределение обслуживает вложенное тело формы using (...), которая может не иметь
         * фигурных скобок.
         */
        var rewritten = (UsingStatementSyntax)base.VisitUsingStatement(node)!;
        return rewritten.WithStatement(
            AddEmbeddedContinuationCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Оборачивает помеченный вложенный оператор, если перед его
    /// await/yield требуется контрольная точка.
    /// </summary>
    /// <param name="node">Исходный помеченный оператор.</param>
    /// <returns>Помеченный оператор с сохранённой меткой и при необходимости новым телом-блоком.</returns>
    public override SyntaxNode? VisitLabeledStatement(LabeledStatementSyntax node)
    {
        /* Контрольная точка помещается внутрь помеченного оператора, поэтому goto продолжает переходить
         * к метке и затем обязательно проходит через вставленную проверку Stop.
         */
        var rewritten = (LabeledStatementSyntax)base.VisitLabeledStatement(node)!;
        return rewritten.WithStatement(
            AddEmbeddedContinuationCheckpoint(rewritten.Statement));
    }

    /// <summary>
    /// Добавляет контрольную точку на входе в метод с телом-блоком либо безопасно преобразует
    /// поддержанный метод с телом-выражением в эквивалентный блок.
    /// </summary>
    /// <remarks>
    /// Семантика возврата и async тела-выражения вычисляется по символу исходного объявления
    /// до рекурсивного преобразования. Пользовательский Task-подобный тип
    /// без доказуемого стандартного поведения остаётся в исходной форме тела-выражения.
    /// </remarks>
    /// <param name="node">Исходное объявление метода.</param>
    /// <returns>Метод с входной контрольной точкой либо неизменённая неподдержанная форма.</returns>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        /* SemanticModel знает символ только исходного узла объявления.
         * Поэтому требуемое поведение тела-выражения фиксируется до базового обхода, создающего
         * новые узлы синтаксиса.
         */
        var expressionBehavior = GetMethodExpressionBehavior(node);
        var rewritten = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

        /* Обычный метод с телом-блоком всегда получает контрольную точку первым оператором.
         * Вспомогательный метод распознаёт уже существующую проверку и не добавляет дубликат.
         */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Объявление abstract/extern/partial не имеет тела. Неподдержанное тело-выражение также
         * сохраняется без рискованного изменения семантики async и return.
         */
        if (rewritten.ExpressionBody is null || expressionBehavior is null)
            return rewritten;

        /* Подтверждённая форма с телом-выражением превращается в блок из контрольной точки и одного
         * оператора return, throw или обычного выражения. Служебные элементы стрелки и точки
         * с запятой переносятся вспомогательным методом.
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
    /// Добавляет входную контрольную точку в локальную функцию по тем же правилам,
    /// что и в обычный метод.
    /// </summary>
    /// <param name="node">Исходное объявление локальной функции.</param>
    /// <returns>Инструментированная либо безопасно сохранённая локальная функция.</returns>
    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        /* Локальная функция имеет собственный IMethodSymbol и отдельную точку входа выполнения
         * выполнения, поэтому контрольная точка нужна при каждом её вызове, а не только
         * на входе во вмещающий метод.
         */
        var expressionBehavior = GetMethodExpressionBehavior(node);
        var rewritten = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;

        /* Тело-блок получает тот же идемпотентный алгоритм входа, что MethodDeclarationSyntax. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Форма только с сигнатурой либо пользовательское Task-подобное тело-выражение остаётся неизменным. */
        if (rewritten.ExpressionBody is null || expressionBehavior is null)
            return rewritten;

        /* Поддержанное тело-выражение преобразуется с заранее вычисленным поведением результата. */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                expressionBehavior.Value))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет контрольную точку на входе в конструктор и преобразует безопасное тело-выражение.
    /// </summary>
    /// <param name="node">Исходное объявление экземплярного или статического конструктора.</param>
    /// <returns>Конструктор с входной контрольной точкой либо неизменённая неподдержанная форма.</returns>
    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        /* Возможность конвертации проверяется на исходном ArrowExpressionClause, пока служебные
         * элементы синтаксиса и директивы ещё принадлежат связанному с SemanticModel дереву
         * дереву SemanticModel.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;

        /* Блок конструктора получает Stop до первого пользовательского оператора тела.
         * Инициализатор конструктора (: base/: this) остаётся вне тела и не переставляется.
         */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Отсутствующее тело или директивы около стрелки запрещают структурное преобразование. */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Конструктор ничего не возвращает, поэтому тело-выражение становится обычным
         * оператором выражения после контрольной точки.
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
    /// Добавляет контрольную точку на входе в пользовательский оператор и преобразует
    /// безопасное тело-выражение в блок с return.
    /// </summary>
    /// <param name="node">Исходное объявление оператора.</param>
    /// <returns>Инструментированное объявление оператора.</returns>
    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        /* Оператор всегда имеет возвращаемое значение, поэтому поддержанное тело-выражение требует
         * оператора return; возможность преобразования зависит прежде всего от директив.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;

        /* Существующий блок получает идемпотентную входную контрольную точку. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Небезопасная форма служебных элементов или директив сохраняется без изменения. */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Выражение либо throw-выражение превращается вспомогательным методом в оператор return/throw. */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Return))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет контрольную точку на входе в оператор преобразования
    /// и преобразует безопасное тело-выражение в блок с return.
    /// </summary>
    /// <param name="node">Исходное объявление неявного/явного оператора преобразования.</param>
    /// <returns>Инструментированный оператор преобразования.</returns>
    public override SyntaxNode? VisitConversionOperatorDeclaration(
        ConversionOperatorDeclarationSyntax node)
    {
        /* Решение о структурном преобразовании принимается по исходным служебным элементам
         * стрелки до базового преобразования.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (ConversionOperatorDeclarationSyntax)
            base.VisitConversionOperatorDeclaration(node)!;

        /* Преобразование с телом-блоком получает контрольную точку до вычисления результата. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Тело-выражение с директивами не перемещается в блок без надёжного сопоставления строк
         * с исходными строками.
         */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Оператор преобразования возвращает значение целевого типа, поэтому используется return. */
        return rewritten
            .WithBody(CreateExpressionBody(
                rewritten.ExpressionBody,
                rewritten.SemicolonToken,
                ExpressionBodyBehavior.Return))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    /// <summary>
    /// Добавляет контрольную точку в метод доступа get/set/init/add/remove и безопасно
    /// преобразует его форму с телом-выражением с учётом возвращаемого поведения.
    /// </summary>
    /// <param name="node">Исходное объявление метода доступа.</param>
    /// <returns>
    /// Инструментированный метод доступа либо исходная форма с точкой с запятой и без тела.
    /// </returns>
    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        /* Метод доступа автоматического свойства или события без тела останется без изменений.
         * Для тела-выражения заранее проверяется отсутствие директив, которые нельзя безопасно
         * переместить.
         */
        var canConvertExpression = CanConvertExpressionBody(node.ExpressionBody);
        var rewritten = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;

        /* Явный блок метода доступа получает контрольную точку перед первым оператором пользователя. */
        if (rewritten.Body is not null)
            return rewritten.WithBody(AddEntryCheckpoint(rewritten.Body));

        /* Форма только с точкой с запятой либо чувствительное к директивам тело-выражение
         * сохраняется как есть.
         */
        if (rewritten.ExpressionBody is null || !canConvertExpression)
            return rewritten;

        /* Метод чтения возвращает значение и требует оператора return. Методы записи,
         * инициализации и доступа к событию ничего не возвращают и используют оператор выражения.
         */
        var behavior = rewritten.Kind() == SyntaxKind.GetAccessorDeclaration
            ? ExpressionBodyBehavior.Return
            : ExpressionBodyBehavior.Expression;

        /* Тело со стрелкой заменяется блоком, а прежние ExpressionBody/Semicolon
         * очищаются, чтобы объявление не содержало две взаимоисключающие формы тела.
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
    /// Добавляет контрольную точку на входе в анонимный метод
    /// <c>delegate { ... }</c>.
    /// </summary>
    /// <param name="node">Исходное выражение анонимного метода.</param>
    /// <returns>Анонимный метод с инструментированным блоком.</returns>
    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        /* AnonymousMethodSyntax всегда имеет BlockSyntax, поэтому анализировать семантику
         * возврата тела-выражения не требуется.
         */
        var rewritten = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    /// <summary>
    /// Добавляет контрольную точку в заключённую в скобки лямбду с телом-блоком; лямбду
    /// с телом-выражением намеренно оставляет
    /// без структурного преобразования.
    /// </summary>
    /// <param name="node">Лямбда со списком параметров в скобках.</param>
    /// <returns>Лямбда с инструментированным блоком либо исходным телом-выражением.</returns>
    public override SyntaxNode? VisitParenthesizedLambdaExpression(
        ParenthesizedLambdaExpressionSyntax node)
    {
        /* Потомки лямбды с телом-выражением всё равно посещаются и могут получить безопасное
         * преобразование вызова, но само выражение не оборачивается без знания семантики целевого
         * целевого делегата.
         */
        var rewritten = (ParenthesizedLambdaExpressionSyntax)
            base.VisitParenthesizedLambdaExpression(node)!;

        /* Block со значением null однозначно обозначает лямбду с телом-выражением. */
        return rewritten.Block is null
            ? rewritten
            : rewritten.WithBlock(AddEntryCheckpoint(rewritten.Block));
    }

    /// <summary>
    /// Добавляет контрольную точку в простую лямбду с телом-блоком;
    /// лямбда с телом-выражением сохраняет свою форму и выведенное преобразование возврата
    /// результата.
    /// </summary>
    /// <param name="node">Лямбда с одним не помещённым в скобки параметром.</param>
    /// <returns>Лямбда с инструментированным блоком либо исходным телом-выражением.</returns>
    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        /* Для лямбды с телом-выражением тип результата определяется целевым делегатом или деревом
         * выражения. Без отдельного семантического доказательства преобразование в блок могло бы
         * изменить выбор перегрузки.
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
    /// Sleep заменяется на <see cref="global::KID.StopManager.Sleep(int)"/>, а Delay получает
    /// именованный аргумент <c>cancellationToken: StopManager.CurrentToken</c>. Остальные выражения
    /// вызова проходят только стандартное рекурсивное преобразование потомков.
    /// </remarks>
    /// <param name="node">Исходное выражение вызова.</param>
    /// <returns>Вызов с поддержкой отмены либо исходная форма.</returns>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        /* Символ запрашивается у исходного узла до базового обхода: SemanticModel не обслуживает
         * новые узлы, которые посетитель создаст для вложенных выражений.
         */
        var method = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;

        /* Сначала сохраняются все изменения дочерних узлов. Затем внешнее преобразование
         * меняет только цель либо список аргументов уже переписанного вызова.
         */
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        /* Thread.Sleep заменяется только при точной идентичности BCL и безопасных служебных
         * элементах цели. Ограничение предотвращает потерю комментариев, переносов строк
         * и директив внутри сложного выражения доступа к члену.
         */
        if (IsThreadSleep(method) && CanReplaceInvocationTarget(node.Expression))
        {
            /* Полностью квалифицированная цель сохраняет выбор перегрузки
             * по существующему аргументу, а WithTriviaFrom переносит внешние служебные элементы
             * исходной цели вызова.
             */
            var target = SyntaxFactory.ParseExpression(SleepTargetCode)
                .WithTriviaFrom(rewritten.Expression);
            return rewritten.WithExpression(target);
        }

        /* Только одноаргументный BCL Task.Delay имеет однозначную перегрузку с поддержкой отмены,
         * получаемую добавлением второго именованного аргумента.
         */
        if (IsSingleArgumentTaskDelay(method))
        {
            /* NameColon делает намерение явным и не зависит от позиционного порядка будущих форм
             * перегрузки. Токен берётся из текущей сессии в момент выполнения кода.
             */
            var tokenArgument = SyntaxFactory.Argument(
                    SyntaxFactory.ParseExpression(CurrentTokenCode))
                .WithNameColon(SyntaxFactory.NameColon("cancellationToken"));

            /* Исходный аргумент и его служебные элементы сохраняются, новый токен добавляется последним. */
            return rewritten.WithArgumentList(
                rewritten.ArgumentList.WithArguments(
                    rewritten.ArgumentList.Arguments.Add(tokenArgument)));
        }

        /* Неразрешённый символ, другая перегрузка или пользовательский API остаются неизменными.
         */
        return rewritten;
    }

    /// <summary>
    /// Определяет, каким оператором должно стать тело-выражение метода или локальной функции
    /// после добавления входной контрольной точки.
    /// </summary>
    /// <param name="node">
    /// Исходный <see cref="MethodDeclarationSyntax"/> или
    /// <see cref="LocalFunctionStatementSyntax"/> из связанной семантической модели.
    /// </param>
    /// <returns>
    /// <see cref="ExpressionBodyBehavior.Expression"/> для void-подобного результата,
    /// <see cref="ExpressionBodyBehavior.Return"/> для возвращаемого значения либо
    /// <see langword="null"/>, если безопасное преобразование не доказано.
    /// </returns>
    private ExpressionBodyBehavior? GetMethodExpressionBehavior(SyntaxNode node)
    {
        /* Вспомогательный метод принимает только два вида объявлений. Для любого другого узла
         * ветвь switch по умолчанию возвращает null и запрещает преобразование.
         */
        var expressionBody = node switch
        {
            MethodDeclarationSyntax method => method.ExpressionBody,
            LocalFunctionStatementSyntax localFunction => localFunction.ExpressionBody,
            _ => null
        };

        /* Директивы около стрелки или тела нельзя перемещать в фигурные скобки без доказанного
         * сопоставления с исходными строками. Если тела-выражения нет, выбирать поведение тоже
         * не требуется.
         */
        if (!CanConvertExpressionBody(expressionBody))
            return null;

        /* Объявленный символ запрашивается у исходного узла. Если Roslyn не может однозначно
         * предоставить IMethodSymbol, текст возвращаемого типа не используется как запасной
         * источник данных.
         */
        var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken) as IMethodSymbol;
        if (symbol is null)
            return null;

        /* Тело-выражение void должно остаться оператором выражения: добавление return было бы
         * синтаксически и семантически неверным.
         */
        if (symbol.ReturnsVoid)
            return ExpressionBodyBehavior.Expression;

        /* Обычный синхронный метод с результатом возвращает значение выражения напрямую. */
        if (!symbol.IsAsync)
            return ExpressionBodyBehavior.Return;

        /* В async Task/ValueTask без результата исходное выражение выполняет действие,
         * поэтому блок использует оператор выражения без return.
         */
        if (IsKnownTaskLike(symbol.ReturnType, arity: 0))
            return ExpressionBodyBehavior.Expression;

        /* В async Task<T>/ValueTask<T> выражение вычисляет логический результат T и должно стать
         * оператором return внутри асинхронного метода.
         */
        if (IsKnownTaskLike(symbol.ReturnType, arity: 1))
            return ExpressionBodyBehavior.Return;

        /* Пользовательские Task-подобные типы зависят от AsyncMethodBuilder и могут иметь отличную
         * семантику преобразования. Без отдельного доказательства они намеренно не переписываются.
         */
        return null;
    }

    /// <summary>
    /// Проверяет стандартную форму Task/ValueTask по пространству имён, имени и количеству
    /// параметров обобщения.
    /// </summary>
    /// <param name="returnType">Символ возвращаемого типа анализируемого async-метода.</param>
    /// <param name="arity">Ожидаемое число аргументов обобщённого типа: 0 либо 1.</param>
    /// <returns>
    /// <see langword="true"/> для стандартного Task/ValueTask; иначе
    /// <see langword="false"/>.
    /// </returns>
    private static bool IsKnownTaskLike(ITypeSymbol returnType, int arity) =>
        /* Проверка намеренно структурная и узкая. Она не пытается интерпретировать пользовательские
         * ожидаемые объекты либо типы Task из другого пространства имён как стандартный
         * асинхронный контракт.
         */
        returnType is INamedTypeSymbol namedType &&
        namedType.Arity == arity &&
        namedType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        namedType.Name is "Task" or "ValueTask";

    /// <summary>
    /// Проверяет, можно ли перенести тело-выражение в блок без перемещения директив.
    /// </summary>
    /// <param name="expressionBody">Секция выражения со стрелкой либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/>, если тело-выражение существует и его стрелка/тело не содержит
    /// директив препроцессора; иначе <see langword="false"/>.
    /// </returns>
    private static bool CanConvertExpressionBody(ArrowExpressionClauseSyntax? expressionBody) =>
        /* Директивы проверяются как во всей секции, так и в ведущих/завершающих служебных элементах
         * токена стрелки. Некоторые проверки могут пересекаться, но явное покрытие границ делает
         * правило устойчивым к разному расположению служебных элементов Roslyn.
         */
        expressionBody is not null &&
        !expressionBody.ContainsDirectives &&
        !expressionBody.ArrowToken.LeadingTrivia.Any(IsDirectiveTrivia) &&
        !expressionBody.ArrowToken.TrailingTrivia.Any(IsDirectiveTrivia);

    /// <summary>
    /// Определяет, представляет ли служебный элемент синтаксиса структурированную директиву
    /// препроцессора.
    /// </summary>
    /// <param name="trivia">Проверяемый служебный элемент токена стрелки.</param>
    /// <returns><see langword="true"/> только для <see cref="DirectiveTriviaSyntax"/>.</returns>
    private static bool IsDirectiveTrivia(SyntaxTrivia trivia) =>
        /* HasStructure избегает ненужного GetStructure для обычных пробелов и комментариев. */
        trivia.HasStructure && trivia.GetStructure() is DirectiveTriviaSyntax;

    /// <summary>
    /// Строит эквивалентный блок из входной контрольной точки и одного оператора
    /// expression/return/throw, сохраняя значимые служебные элементы.
    /// </summary>
    /// <param name="expressionBody">Подтверждённая безопасная секция выражения со стрелкой.</param>
    /// <param name="semicolonToken">Исходная завершающая точка с запятой объявления.</param>
    /// <param name="behavior">
    /// Требование представить обычное выражение оператором выражения или оператором return.
    /// </param>
    /// <returns>Новый блок, заменяющий тело-выражение объявления.</returns>
    private static BlockSyntax CreateExpressionBody(
        ArrowExpressionClauseSyntax expressionBody,
        SyntaxToken semicolonToken,
        ExpressionBodyBehavior behavior)
    {
        /* Завершающие служебные элементы исходной точки с запятой должны
         * принадлежать закрывающей фигурной скобке всего блока, а не внутреннему оператору.
         * Поэтому для оператора создаётся токен без завершающих служебных элементов.
         */
        var statementSemicolon = semicolonToken.WithTrailingTrivia();

        /* Throw-выражение во всех поддержанных формах объявления превращается в оператор throw.
         * Остальные выражения следуют заранее определённому поведению возврата.
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

        /* Ведущие/завершающие служебные элементы токена стрелки переносятся на открывающую
         * фигурную скобку. Это сохраняет пробелы и комментарии вокруг прежнего => без добавления
         * новых строк исходного кода.
         */
        var openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
            .WithLeadingTrivia(expressionBody.ArrowToken.LeadingTrivia)
            .WithTrailingTrivia(expressionBody.ArrowToken.TrailingTrivia);

        /* Блок содержит ровно два оператора: стабильную контрольную точку Stop и исходное
         * вычисление. Завершающие служебные элементы прежней точки с запятой переносятся
         * на закрывающую фигурную скобку объявления.
         */
        return SyntaxFactory.Block(CreateStopCheck(), expressionStatement)
            .WithOpenBraceToken(openBrace)
            .WithCloseBraceToken(
                SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithTrailingTrivia(semicolonToken.TrailingTrivia));
    }

    /// <summary>
    /// Вставляет контрольную точку Stop первым оператором блока, если она ещё не присутствует.
    /// </summary>
    /// <param name="block">Блок вызываемой конструкции или тело цикла.</param>
    /// <returns>Исходный идемпотентный блок либо блок с добавленным первым оператором.</returns>
    private static BlockSyntax AddEntryCheckpoint(BlockSyntax block)
    {
        /* Структурная эквивалентность с канонической контрольной точкой
         * предотвращает повторную вставку при повторном запуске преобразователя над уже
         * инструментированным исходным кодом.
         */
        if (block.Statements.FirstOrDefault() is { } firstStatement &&
            IsStopCheck(firstStatement))
        {
            return block;
        }

        /* Insert сохраняет относительный порядок всех пользовательских операторов и скобок блока. */
        return block.WithStatements(block.Statements.Insert(0, CreateStopCheck()));
    }

    /// <summary>
    /// Гарантирует контрольную точку в начале тела цикла, сохраняя блок либо оборачивая одиночный
    /// вложенный оператор в новый блок.
    /// </summary>
    /// <param name="statement">Переписанное тело while/do/for/foreach.</param>
    /// <returns>Оператор с телом-блоком либо уже канонический оператор контрольной точки.</returns>
    private static StatementSyntax AddLoopCheckpoint(StatementSyntax statement)
    {
        /* Существующий блок использует общий алгоритм входа и не получает лишнего уровня скобок. */
        if (statement is BlockSyntax block)
            return AddEntryCheckpoint(block);

        /* Защитная ветвь идемпотентности сохраняет отдельную каноническую проверку без обёртки. */
        if (IsStopCheck(statement))
            return statement;

        /* Вложенный оператор оборачивается в блок, где контрольная точка всегда выполняется первой,
         * а исходный оператор остаётся вторым без изменения своей внутренней структуры.
         */
        return SyntaxFactory.Block(CreateStopCheck(), statement);
    }

    /// <summary>
    /// Добавляет контрольную точку к вложенному оператору с await/yield, если он не имеет
    /// собственного блока и действительно является точкой продолжения.
    /// </summary>
    /// <param name="statement">Оператор ветви if/else, using либо метки.</param>
    /// <returns>Исходный оператор либо новый блок из контрольной точки и оператора.</returns>
    private static StatementSyntax AddEmbeddedContinuationCheckpoint(
        StatementSyntax statement)
    {
        /* Блок уже обработан VisitBlock. Оператор без await/yield не должен получать лишние
         * скобки и точки отмены за пределами согласованной области инструментирования.
         */
        if (statement is BlockSyntax || !NeedsContinuationCheck(statement))
            return statement;

        /* Обёртка сохраняет вложенный оператор как единое тело родительской конструкции. */
        return SyntaxFactory.Block(CreateStopCheck(), statement);
    }

    /// <summary>
    /// Вставляет контрольную точку непосредственно перед каждым поддержанным оператором await/yield
    /// в упорядоченном списке, не создавая соседние дубликаты.
    /// </summary>
    /// <param name="statements">Операторы обычного блока или секции switch.</param>
    /// <returns>Новый неизменяемый список операторов с сохранённым порядком пользователя.</returns>
    private static SyntaxList<StatementSyntax> AddContinuationChecks(
        SyntaxList<StatementSyntax> statements)
    {
        /* Изменяемый список упрощает условную вставку нескольких элементов,
         * после чего результат снова превращается в неизменяемый SyntaxList Roslyn.
         */
        var rewritten = new List<StatementSyntax>(statements.Count);

        foreach (var statement in statements)
        {
            /* Контрольная точка добавляется только для кандидата и только если предыдущим элементом
             * ещё не является каноническая проверка. Это объединяет проверку входа в метод или цикл
             * на входе с первым оператором await и обеспечивает
             * идемпотентность повторного прохода.
             */
            if (NeedsContinuationCheck(statement) &&
                (rewritten.LastOrDefault() is not { } previous || !IsStopCheck(previous)))
            {
                rewritten.Add(CreateStopCheck());
            }

            /* Пользовательский оператор всегда добавляется ровно один раз и после своей проверки. */
            rewritten.Add(statement);
        }

        /* SyntaxFactory.List фиксирует окончательный порядок в неизменяемом списке.
         */
        return SyntaxFactory.List(rewritten);
    }

    /// <summary>
    /// Определяет, является ли оператор безопасно поддержанным кандидатом точки продолжения
    /// await/yield.
    /// </summary>
    /// <remarks>
    /// Поиск await не спускается во вложенные лямбды и локальные функции: у них отдельная точка
    /// входа и собственное правило инструментирования. Составные операторы анализируются
    /// специализированными Visit*-методами, чтобы контрольная
    /// точка оказалась в правильной ветви или теле.
    /// </remarks>
    /// <param name="statement">Проверяемый оператор после рекурсивного преобразования потомков.</param>
    /// <returns>
    /// <see langword="true"/>, если непосредственно перед оператором нужна контрольная точка.
    /// </returns>
    private static bool NeedsContinuationCheck(StatementSyntax statement)
    {
        /* Любой yield return/yield break является явной границей приостановки итератора
         * приостановки итератора.
         */
        if (statement is YieldStatementSyntax)
            return true;

        /* Объявление await using представлено LocalDeclarationStatementSyntax с AwaitKeyword,
         * даже если среди потомков отсутствует AwaitExpressionSyntax.
         */
        if (statement is LocalDeclarationStatementSyntax localDeclaration &&
            localDeclaration.AwaitKeyword != default)
        {
            return true;
        }

        /* Форма await using как оператора хранит AwaitKeyword непосредственно в UsingStatementSyntax. */
        if (statement is UsingStatementSyntax usingStatement &&
            usingStatement.AwaitKeyword != default)
        {
            return true;
        }

        /* Ограниченный белый список форм операторов исключает
         * if/loops/try и другие составные узлы: корректная позиция контрольной точки определяется
         * соответствующим переопределением Visit.
         */
        if (statement is not ExpressionStatementSyntax and
            not LocalDeclarationStatementSyntax and
            not ReturnStatementSyntax and
            not ThrowStatementSyntax)
        {
            return false;
        }

        /* Для простых форм операторов ищется любой AwaitExpression, но обход прекращается перед
         * вложенными исполняемыми областями, которые не выполняются
         * непосредственно этим оператором.
         */
        return statement.DescendantNodes(
                ShouldDescendIntoContinuationCandidate)
            .OfType<AwaitExpressionSyntax>()
            .Any();
    }

    /// <summary>
    /// Ограничивает обход потомков кандидата точки продолжения текущей исполняемой областью.
    /// </summary>
    /// <param name="node">Потенциальный потомок проверяемого оператора.</param>
    /// <returns>
    /// <see langword="false"/> на границе вложенной лямбды или локальной функции; иначе
    /// <see langword="true"/>.
    /// </returns>
    private static bool ShouldDescendIntoContinuationCandidate(SyntaxNode node) =>
        /* Await внутри лямбды или локальной функции выполняется только при отдельном будущем вызове
         * и не должен заставлять вмещающий оператор объявления получать проверку продолжения.
         */
        node is not AnonymousFunctionExpressionSyntax and
        not LocalFunctionStatementSyntax;

    /// <summary>
    /// Сравнивает оператор с канонической автоматически генерируемой контрольной точкой Stop.
    /// </summary>
    /// <param name="statement">Проверяемый синтаксический оператор.</param>
    /// <returns><see langword="true"/> при структурной эквивалентности без учёта режима верхнего уровня.</returns>
    private static bool IsStopCheck(StatementSyntax statement) =>
        /* IsEquivalentTo сравнивает структуру синтаксиса, а не идентичность
         * объекта. Поэтому проверка, восстановленная синтаксическим разбором повторного прохода,
         * распознаётся так же, как только что созданная.
         */
        statement.IsEquivalentTo(CreateStopCheck(), topLevel: false);

    /// <summary>
    /// Создаёт канонический полностью квалифицированный оператор проверки Stop.
    /// </summary>
    /// <returns>Новый оператор-выражение без пользовательских служебных элементов синтаксиса.</returns>
    private static ExpressionStatementSyntax CreateStopCheck() =>
        /* ParseStatement централизует точную форму генерируемого кода,
         * используемую одновременно для вставки и проверки идемпотентности.
         */
        (ExpressionStatementSyntax)SyntaxFactory.ParseStatement(StopCheckCode);

    /// <summary>
    /// Проверяет, можно ли заменить цель вызова Thread.Sleep без потери внутренних служебных
    /// элементов синтаксиса.
    /// </summary>
    /// <param name="expression">Исходная цель вызова до рекурсивного преобразования.</param>
    /// <returns>
    /// <see langword="true"/>, если цель не содержит переносов строк, директив или комментариев;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool CanReplaceInvocationTarget(ExpressionSyntax expression) =>
        /* При полной замене цели негде надёжно разместить служебные элементы внутри цепочки
         * доступа к членам. Поэтому редкие формы с чувствительным форматированием остаются
         * неизменными: так комментарий или директива не потеряются.
         */
        !expression.DescendantTrivia(descendIntoTrivia: true)
            .Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia) ||
                           trivia.IsDirective ||
                           trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                           trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

    /// <summary>
    /// Проверяет поддержанную перегрузку настоящего BCL Thread.Sleep.
    /// </summary>
    /// <param name="method">Разрешённый символ вызываемого метода либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> для статического Sleep(int/TimeSpan) настоящего типа Thread; иначе
    /// <see langword="false"/>.
    /// </returns>
    private bool IsThreadSleep(IMethodSymbol? method) =>
        /* Проверка формы отсеивает перегрузки до более дорогого сравнения символов.
         * Поддержанный параметр ограничивает преобразование формами, точно
         * представленными StopManager.
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
    /// Проверяет одноаргументную поддержанную перегрузку настоящего BCL Task.Delay.
    /// </summary>
    /// <param name="method">Разрешённый символ вызываемого метода либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> для статического Delay(int/TimeSpan) настоящего типа Task; иначе
    /// <see langword="false"/>.
    /// </returns>
    private bool IsSingleArgumentTaskDelay(IMethodSymbol? method) =>
        /* Уже учитывающая отмену двухаргументная перегрузка намеренно не изменяется. Один аргумент
         * гарантирует, что добавление именованного cancellationToken приводит к известной форме BCL.
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
    /// Проверяет параметр времени, общий для поддержанных перегрузок Thread.Sleep и Task.Delay.
    /// </summary>
    /// <param name="type">Roslyn-символ типа единственного аргумента.</param>
    /// <returns><see langword="true"/> только для <see cref="int"/> или <see cref="TimeSpan"/>.</returns>
    private static bool IsSupportedDelayParameter(ITypeSymbol type) =>
        /* Int32 надёжно определяется через SpecialType, а TimeSpan сравнивается в полностью
         * квалифицированной форме, исключающей влияние псевдонимов и одноимённых пространств имён.
         */
        type.SpecialType == SpecialType.System_Int32 ||
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
        "global::System.TimeSpan";

    /// <summary>
    /// Описывает семантику исходного тела-выражения после
    /// преобразования в блок.
    /// </summary>
    private enum ExpressionBodyBehavior
    {
        /// <summary>
        /// Выражение выполняется ради побочного эффекта и не возвращает значение.
        /// </summary>
        Expression,

        /// <summary>
        /// Значение выражения возвращается из метода, оператора или getter через return.
        /// </summary>
        Return
    }
}

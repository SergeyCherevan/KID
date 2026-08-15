using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Services.CodeExecution.Rewriters;

/// <summary>
/// Заменяет подтверждённый вызов BCL <see cref="Console.Clear"/> публичным мостом консоли WPF
/// в KID, не изменяя одноимённые пользовательские типы и методы.
/// </summary>
/// <remarks>
/// <para>
/// Класс является узкоспециализированным этапом конвейера и использует паттерн
/// <b>Visitor (Посетитель)</b> через <see cref="CSharpSyntaxRewriter"/>.
/// Посетитель создаёт новое неизменяемое синтаксическое дерево,
/// а исходное дерево и его <see cref="SemanticModel"/> остаются неизменными.
/// </para>
/// <para>
/// Решение о замене принимается по <see cref="IMethodSymbol"/>, а не по тексту выражения.
/// Дополнительно через <see cref="RuntimeTypeSymbolResolver"/> проверяется сборка, которой
/// принадлежит тип <see cref="Console"/>. Поэтому пользовательский класс с именем
/// <c>System.Console</c> не может ошибочно считаться настоящим BCL-типом.
/// </para>
/// <para>
/// Преобразователь заменяет только цель вызова и переносит на неё исходные пробелы, комментарии
/// и директивы. Аргументы и остальная структура программы обходятся стандартным механизмом
/// Roslyn без специальных изменений.
/// </para>
/// </remarks>
internal sealed class ConsoleClearRewriter : CSharpSyntaxRewriter
{
    // Полностью квалифицированная цель исключает зависимость сгенерированного кода от
    // пользовательских директив using и направляет Clear в TextBoxConsole текущей сессии.
    private const string ConsoleClearTarget =
        "global::KID.Services.CodeExecution.TextBoxConsole.StaticConsole.Clear";

    // SemanticModel принадлежит ровно тому SyntaxTree, узлы которого передаются посетителю.
    // Токен позволяет остановить обход вместе с текущей сессией выполнения.
    private readonly SemanticModel semanticModel;
    private readonly CancellationToken cancellationToken;

    // Этот символ фиксирует не только имя System.Console, но и его сборку, поэтому одноимённый
    // пользовательский тип не будет ошибочно преобразован.
    private readonly INamedTypeSymbol? systemConsoleType;

    /// <summary>
    /// Создаёт семантический посетитель для одного конкретного синтаксического дерева.
    /// </summary>
    /// <param name="semanticModel">
    /// Модель Roslyn, построенная для дерева, которое будет передано в <see cref="Visit"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен сессии выполнения, проверяемый при посещении каждого узла и разрешении символов.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="semanticModel"/> имеет значение <see langword="null"/>.
    /// </exception>
    public ConsoleClearRewriter(
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        /* Без SemanticModel невозможно отличить настоящий Console.Clear от метода с тем же
         * текстовым именем. Немедленный отказ в конструкторе сохраняет это условие для всех
         * экземпляров класса.
         */
        this.semanticModel = semanticModel ??
            throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;

        /* Тип Console разрешается один раз для всего прохода, а не для каждого вызова.
         * null означает, что компиляция не содержит подходящей ссылки на сборку; в этом случае
         * преобразователь безопасно оставит все вызовы неизменными.
         */
        systemConsoleType = RuntimeTypeSymbolResolver.Resolve(
            semanticModel.Compilation,
            typeof(Console));
    }

    /// <summary>
    /// Проверяет токен сессии перед стандартным посещением каждого узла синтаксиса.
    /// </summary>
    /// <remarks>
    /// Переопределение образует единую точку отмены для всего рекурсивного обхода. Конкретные
    /// Visit*-методы не обязаны
    /// дублировать проверку перед каждым дочерним узлом.
    /// </remarks>
    /// <param name="node">Текущий узел либо <see langword="null"/> по контракту Roslyn.</param>
    /// <returns>
    /// Переписанный узел, исходный узел без изменений либо <see langword="null"/>, если это
    /// допускает базовый посетитель.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Для текущей сессии уже запрошен Stop.
    /// </exception>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        /* Проверка выполняется до входа в узел: после Stop посетитель не начинает обход нового
         * поддерева и разматывает рекурсию стандартным исключением отмены.
         */
        cancellationToken.ThrowIfCancellationRequested();
        return base.Visit(node);
    }

    /// <summary>
    /// Семантически анализирует вызов и при точном совпадении заменяет
    /// <see cref="Console.Clear"/> мостом WPF-консоли.
    /// </summary>
    /// <param name="node">Исходный узел вызова из дерева связанной семантической модели.</param>
    /// <returns>
    /// Переписанный вызов с прежними аргументами и служебными элементами синтаксиса либо
    /// неизменённый вызов.
    /// </returns>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        /* Символ запрашивается у исходного узла до рекурсивного преобразования: SemanticModel
         * привязана именно к исходному дереву и не знает узлы, созданные базовым посетителем.
         */
        var method = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol as IMethodSymbol;

        /* Сначала преобразуются вложенные вызовы. Полученный узел используется как основа,
         * чтобы изменения дочерних узлов не потерялись при замене внешней цели.
         */
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        /* Неоднозначный, неразрешённый, пользовательский вызов или неподходящая перегрузка
         * остаются без специального преобразования.
         */
        if (!IsSystemConsoleClear(method))
            return rewritten;

        /* ParseExpression создаёт полностью квалифицированную цель без зависимости от
         * пользовательских псевдонимов и директив using. WithTriviaFrom сохраняет комментарии
         * и пробелы исходного выражения, а WithExpression оставляет список аргументов прежним.
         */
        var target = SyntaxFactory.ParseExpression(ConsoleClearTarget)
            .WithTriviaFrom(rewritten.Expression);
        return rewritten.WithExpression(target);
    }

    /// <summary>
    /// Проверяет точную сигнатуру и сборку, которой принадлежит метод BCL Console.Clear().
    /// </summary>
    /// <param name="method">Разрешённый Roslyn-символ метода либо <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> только для статического безаргументного метода Clear настоящего
    /// типа <see cref="Console"/> из BCL; иначе <see langword="false"/>.
    /// </returns>
    private bool IsSystemConsoleClear(IMethodSymbol? method) =>
        /* Шаблон свойств сначала дёшево проверяет форму метода. SymbolEqualityComparer затем
         * сравнивает символы Roslyn с учётом текущей компиляции, а не только по отображаемому имени.
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

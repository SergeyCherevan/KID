using KID.Services.CodeExecution.Rewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Tests.Compiler;

public sealed class CancellationInstrumentationTests
{
    [Fact]
    public void Rewriter_InsertsCheckpointAtEveryLoopEntry()
    {
        const string code = """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            class Program
            {
                static async Task Main()
                {
                    while (true) { }
                    do Work(); while (true);
                    for (;;) ;
                    foreach (var item in Items()) Consume(item);
                    foreach (var (left, right) in Pairs()) Consume(left + right);
                    await foreach (var item in AsyncItems()) { Consume(item); }
                }

                static void Work() { }
                static void Consume(int value) { }
                static IEnumerable<int> Items() => [];
                static IEnumerable<(int Left, int Right)> Pairs() => [];
                static async IAsyncEnumerable<int> AsyncItems()
                {
                    await Task.CompletedTask;
                    yield break;
                }
            }
            """;

        var rewritten = RewriteCancellation(code);

        foreach (var loop in rewritten.DescendantNodes().OfType<WhileStatementSyntax>())
            AssertStartsWithCheckpoint(loop.Statement);
        foreach (var loop in rewritten.DescendantNodes().OfType<DoStatementSyntax>())
            AssertStartsWithCheckpoint(loop.Statement);
        foreach (var loop in rewritten.DescendantNodes().OfType<ForStatementSyntax>())
            AssertStartsWithCheckpoint(loop.Statement);
        foreach (var loop in rewritten.DescendantNodes().OfType<ForEachStatementSyntax>())
            AssertStartsWithCheckpoint(loop.Statement);
        foreach (var loop in rewritten.DescendantNodes().OfType<ForEachVariableStatementSyntax>())
            AssertStartsWithCheckpoint(loop.Statement);

        var awaitForEach = Assert.Single(
            rewritten.DescendantNodes().OfType<ForEachStatementSyntax>(),
            loop => loop.AwaitKeyword != default);
        AssertStartsWithCheckpoint(awaitForEach.Statement);
    }

    [Fact]
    public void Rewriter_InstrumentsNestedAndEmptyLoopBodies()
    {
        const string code = """
            class Program
            {
                static void Main()
                {
                    while (true)
                        for (;;)
                            do ; while (true);
                }
            }
            """;

        var rewritten = RewriteCancellation(code);
        var loops = rewritten.DescendantNodes()
            .Where(node => node is WhileStatementSyntax or
                           ForStatementSyntax or
                           DoStatementSyntax)
            .ToArray();

        Assert.Equal(3, loops.Length);
        foreach (var loop in loops)
        {
            var statement = loop switch
            {
                WhileStatementSyntax whileStatement => whileStatement.Statement,
                ForStatementSyntax forStatement => forStatement.Statement,
                DoStatementSyntax doStatement => doStatement.Statement,
                _ => throw new InvalidOperationException()
            };
            AssertStartsWithCheckpoint(statement);
        }

        Assert.Contains(
            rewritten.DescendantNodes().OfType<EmptyStatementSyntax>(),
            _ => true);
    }

    [Fact]
    public void Rewriter_InstrumentsTopLevelProgramEntry()
    {
        const string code = """
            var value = 0;
            while (value < 1) value++;
            """;

        var rewritten = RewriteCancellation(code);
        var firstGlobalStatement = Assert.IsType<GlobalStatementSyntax>(
            rewritten.Members.First());

        Assert.True(IsCheckpoint(firstGlobalStatement.Statement));
        var loop = Assert.Single(
            rewritten.DescendantNodes().OfType<WhileStatementSyntax>());
        AssertStartsWithCheckpoint(loop.Statement);
    }

    [Fact]
    public void Rewriter_InstrumentsSupportedBlockBodiedCallables()
    {
        const string code = """
            using System;

            class Value
            {
                public Value() { }
                public int Property { get { return 1; } set { } }
                public void Method()
                {
                    void Local() { }
                    Action simple = () => { };
                    Action parenthesized = () => { };
                    Action anonymous = delegate { };
                }
                public static Value operator +(Value left, Value right) { return left; }
                public static implicit operator int(Value value) { return 0; }
            }
            """;

        var rewritten = RewriteCancellation(code);
        var callableBodies = new List<BlockSyntax>();

        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(method => method.Body!)
            .Where(body => body is not null));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Select(local => local.Body!)
            .Where(body => body is not null));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Select(constructor => constructor.Body!)
            .Where(body => body is not null));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<OperatorDeclarationSyntax>()
            .Select(@operator => @operator.Body!)
            .Where(body => body is not null));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<ConversionOperatorDeclarationSyntax>()
            .Select(conversion => conversion.Body!)
            .Where(body => body is not null));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<AccessorDeclarationSyntax>()
            .Select(accessor => accessor.Body!)
            .Where(body => body is not null));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<AnonymousMethodExpressionSyntax>()
            .Select(anonymous => anonymous.Block));
        callableBodies.AddRange(rewritten.DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Select(lambda => lambda.Body)
            .OfType<BlockSyntax>());

        Assert.NotEmpty(callableBodies);
        foreach (var body in callableBodies)
            Assert.True(IsCheckpoint(body.Statements.FirstOrDefault()));
    }

    [Fact]
    public void Rewriter_ConvertsSupportedExpressionBodiesAndLeavesPropertyBodyUnchanged()
    {
        const string code = """
            using System;
            using System.Threading.Tasks;

            class Value
            {
                private int field;
                public Value() => field = 1;
                public int Read() => field;
                public void Write() => Console.WriteLine(field);
                public async Task WaitAsync() => await Task.Delay(1);
                public async Task<int> ReadAsync() => await Task.FromResult(field);
                public int Property => field;
                public int Accessor { get => field; set => field = value; }
                public static Value operator +(Value left, Value right) => left;
                public static implicit operator int(Value value) => value.field;
            }
            """;

        var rewritten = RewriteCancellation(code);

        foreach (var method in rewritten.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            Assert.Null(method.ExpressionBody);
            Assert.NotNull(method.Body);
            Assert.True(IsCheckpoint(method.Body.Statements.FirstOrDefault()));
        }

        var waitAsync = rewritten.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "WaitAsync");
        Assert.IsType<ExpressionStatementSyntax>(waitAsync.Body!.Statements[1]);

        var readAsync = rewritten.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "ReadAsync");
        Assert.IsType<ReturnStatementSyntax>(readAsync.Body!.Statements[1]);

        var property = rewritten.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == "Property");
        Assert.NotNull(property.ExpressionBody);

        foreach (var accessor in rewritten.DescendantNodes().OfType<AccessorDeclarationSyntax>())
        {
            Assert.Null(accessor.ExpressionBody);
            Assert.True(IsCheckpoint(accessor.Body!.Statements.FirstOrDefault()));
        }
    }

    [Fact]
    public void Rewriter_AddsCheckpointBeforeStatementLevelAwaitAndYield()
    {
        const string code = """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            class Program
            {
                static async Task RunAsync()
                {
                    await Task.Delay(1);
                    var value = await Task.FromResult(1);
                }

                static IEnumerable<int> Values()
                {
                    yield return 1;
                    yield break;
                }
            }
            """;

        var rewritten = RewriteCancellation(code);

        foreach (var body in rewritten.DescendantNodes().OfType<MethodDeclarationSyntax>()
                     .Select(method => method.Body!))
        {
            for (var index = 0; index < body.Statements.Count; index++)
            {
                var statement = body.Statements[index];
                if (!ContainsStatementLevelAwait(statement) && statement is not YieldStatementSyntax)
                    continue;

                Assert.True(index > 0);
                Assert.True(IsCheckpoint(body.Statements[index - 1]));
            }
        }
    }

    [Fact]
    public void Rewriter_InstrumentsAwaitInEmbeddedStatements()
    {
        const string code = """
            using System.IO;
            using System.Threading.Tasks;

            class Program
            {
                static async Task RunAsync(bool condition)
                {
                    if (condition)
                        await Task.Delay(1);
                    else
                        await Task.Delay(2);

                retry:
                    await Task.Delay(3);

                    using (new MemoryStream())
                        await Task.Delay(4);
                }
            }
            """;

        var rewritten = RewriteCancellation(code);
        var ifStatement = Assert.Single(
            rewritten.DescendantNodes().OfType<IfStatementSyntax>());
        var labeledStatement = Assert.Single(
            rewritten.DescendantNodes().OfType<LabeledStatementSyntax>());
        var usingStatement = Assert.Single(
            rewritten.DescendantNodes().OfType<UsingStatementSyntax>());

        AssertStartsWithCheckpoint(ifStatement.Statement);
        Assert.NotNull(ifStatement.Else);
        AssertStartsWithCheckpoint(ifStatement.Else.Statement);
        AssertStartsWithCheckpoint(labeledStatement.Statement);
        AssertStartsWithCheckpoint(usingStatement.Statement);
    }

    [Fact]
    public void Rewriter_DoesNotInstrumentFinallyOrFinalizerCleanup()
    {
        const string code = """
            using System;
            using System.Threading;

            class Program
            {
                ~Program()
                {
                    while (true) Thread.Sleep(1);
                }

                static void Main()
                {
                    try { }
                    finally
                    {
                        while (true) Thread.Sleep(1);
                    }
                }
            }
            """;

        var rewritten = RewriteCancellation(code);
        var finalizer = Assert.Single(
            rewritten.DescendantNodes().OfType<DestructorDeclarationSyntax>());
        var finallyClause = Assert.Single(
            rewritten.DescendantNodes().OfType<FinallyClauseSyntax>());

        Assert.DoesNotContain(
            finalizer.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => invocation.ToString().Contains("StopManager", StringComparison.Ordinal));
        Assert.DoesNotContain(
            finallyClause.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => invocation.ToString().Contains("StopManager", StringComparison.Ordinal));
        Assert.All(
            finalizer.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Concat(finallyClause.DescendantNodes().OfType<InvocationExpressionSyntax>()),
            invocation => Assert.Contains("Thread.Sleep", invocation.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Rewriter_IsIdempotent()
    {
        const string code = """
            using System.Threading.Tasks;

            class Program
            {
                static async Task Main()
                {
                    while (true) await Task.Delay(1);
                }
            }
            """;

        var firstPass = RewriteCancellation(code);
        var secondPass = RewriteCancellation(firstPass.ToFullString());

        Assert.Equal(firstPass.ToFullString(), secondPass.ToFullString());
    }

    [Fact]
    public void Rewriter_RewritesOnlyResolvedBclSleepAndDelayCalls()
    {
        const string code = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using BclTask = System.Threading.Tasks.Task;
            using BclThread = System.Threading.Thread;

            class Program
            {
                static async BclTask Main()
                {
                    BclThread.Sleep(1);
                    BclThread.Sleep(TimeSpan.FromMilliseconds(1));
                    await BclTask.Delay(1);
                    await BclTask.Delay(TimeSpan.FromMilliseconds(1));
                    await BclTask.Delay(1, CancellationToken.None);
                    UserThread.Sleep(1);
                    await UserTask.Delay(1);
                }
            }

            static class UserThread
            {
                public static void Sleep(int milliseconds) { }
            }

            static class UserTask
            {
                public static BclTask Delay(int milliseconds) => BclTask.CompletedTask;
            }
            """;

        var rewritten = RewriteCancellation(code);
        var invocations = rewritten.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => invocation.ToString())
            .ToArray();

        Assert.Equal(
            2,
            invocations.Count(invocation => invocation.StartsWith(
                "global::KID.StopManager.Sleep(",
                StringComparison.Ordinal)));
        Assert.Equal(
            2,
            invocations.Count(invocation => invocation.Contains(
                "cancellationToken:global::KID.StopManager.CurrentToken",
                StringComparison.Ordinal)));
        Assert.Contains(
            invocations,
            invocation => invocation.Contains(
                "BclTask.Delay(1, CancellationToken.None)",
                StringComparison.Ordinal));
        Assert.Contains(
            invocations,
            invocation => invocation.Contains("UserThread.Sleep(1)", StringComparison.Ordinal));
        Assert.Contains(
            invocations,
            invocation => invocation.Contains("UserTask.Delay(1)", StringComparison.Ordinal));
    }

    [Fact]
    public void Rewriter_DoesNotRewriteSourceTypesThatShadowBclMetadataNames()
    {
        const string code = """
            namespace System.Threading
            {
                static class Thread
                {
                    public static void Sleep(int milliseconds) { }
                }
            }

            namespace System.Threading.Tasks
            {
                static class Task
                {
                    public static int Delay(int milliseconds) => milliseconds;
                }
            }

            class Program
            {
                static void Main()
                {
                    global::System.Threading.Thread.Sleep(1);
                    global::System.Threading.Tasks.Task.Delay(1);
                }
            }
            """;

        var rewritten = RewriteCancellation(code).ToFullString();

        Assert.Contains(
            "global::System.Threading.Thread.Sleep(1)",
            rewritten,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::System.Threading.Tasks.Task.Delay(1)",
            rewritten,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "StopManager.Sleep",
            rewritten,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "cancellationToken:global::KID.StopManager.CurrentToken",
            rewritten,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rewriter_PreservesDirectivesAndSourceLineCount()
    {
        const string code = """
            class Program
            {
                static void Main()
                {
            #if FEATURE
                    while (true) { }
            #endif
                    MissingSymbol();
                }
            }
            """;

        var rewritten = RewriteCancellation(code);
        var rewrittenCode = rewritten.ToFullString();

        Assert.Contains("#if FEATURE", rewrittenCode, StringComparison.Ordinal);
        Assert.Contains("#endif", rewrittenCode, StringComparison.Ordinal);
        Assert.Equal(CountLines(code), CountLines(rewrittenCode));
    }

    [Fact]
    public void Rewriter_ExpressionBodyBeforeDirectiveRemainsValidAndLineStable()
    {
        const string code = """
            class Program
            {
                static int Value() => 1;
            #if FEATURE
                static int Other() => 2;
            #endif
                static void Main() { }
            }
            """;

        var rewrittenCode = RewriteCancellation(code).ToFullString();
        var (_, semanticModel) = CreateSemanticModel(rewrittenCode);
        var errors = semanticModel.Compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
        Assert.Equal(CountLines(code), CountLines(rewrittenCode));
    }

    private static CompilationUnitSyntax RewriteCancellation(string code)
    {
        var (tree, semanticModel) = CreateSemanticModel(code);
        var root = tree.GetCompilationUnitRoot();
        return (CompilationUnitSyntax)new CancellationInstrumentationRewriter(
            semanticModel).Visit(root)!;
    }

    internal static (SyntaxTree Tree, SemanticModel SemanticModel) CreateSemanticModel(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(StopManager).Assembly)
            .Append(typeof(CancellationInstrumentationRewriter).Assembly)
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .DistinctBy(assembly => assembly.Location, StringComparer.OrdinalIgnoreCase);
        var references = assemblies
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create(
            $"RewriterTests_{Guid.NewGuid():N}",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        return (tree, compilation.GetSemanticModel(tree, ignoreAccessibility: true));
    }

    internal static bool IsCheckpoint(StatementSyntax? statement) =>
        statement is ExpressionStatementSyntax expressionStatement &&
        expressionStatement.Expression is InvocationExpressionSyntax invocation &&
        invocation.Expression.ToString() ==
        "global::KID.StopManager.StopIfButtonPressed";

    private static void AssertStartsWithCheckpoint(StatementSyntax statement)
    {
        var block = Assert.IsType<BlockSyntax>(statement);
        Assert.True(IsCheckpoint(block.Statements.FirstOrDefault()));
    }

    private static bool ContainsStatementLevelAwait(StatementSyntax statement) =>
        statement is ExpressionStatementSyntax or
            LocalDeclarationStatementSyntax or
            ReturnStatementSyntax or
            ThrowStatementSyntax &&
        statement.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();

    private static int CountLines(string text) =>
        text.Count(character => character == '\n') + 1;
}

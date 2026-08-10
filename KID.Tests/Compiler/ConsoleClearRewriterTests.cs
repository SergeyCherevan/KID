using KID.Services.CodeExecution.Rewriters;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KID.Tests.Compiler;

public sealed class ConsoleClearRewriterTests
{
    [Fact]
    public void Rewriter_ReplacesSystemConsoleClear()
    {
        const string code = """
            using System;
            class Program { static void Main() { Console.Clear(); } }
            """;
        var (tree, semanticModel) =
            CancellationInstrumentationTests.CreateSemanticModel(code);

        var rewritten = new ConsoleClearRewriter(semanticModel)
            .Visit(tree.GetRoot(TestContext.Current.CancellationToken))!;
        var invocation = Assert.Single(
            rewritten.DescendantNodes().OfType<InvocationExpressionSyntax>());

        Assert.Equal(
            "global::KID.Services.CodeExecution.TextBoxConsole.StaticConsole.Clear()",
            invocation.ToString());
    }

    [Fact]
    public void Rewriter_DoesNotReplaceSameNamedUserMethod()
    {
        const string code = """
            class Console { public static void Clear() { } }
            class Program { static void Main() { Console.Clear(); } }
            """;
        var (tree, semanticModel) =
            CancellationInstrumentationTests.CreateSemanticModel(code);

        var rewritten = new ConsoleClearRewriter(semanticModel)
            .Visit(tree.GetRoot(TestContext.Current.CancellationToken))!;
        var invocations = rewritten.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();

        Assert.Contains(
            invocations,
            invocation => invocation.ToString() == "Console.Clear()");
    }

    [Fact]
    public void Rewriter_DoesNotReplaceSourceTypeThatShadowsSystemConsole()
    {
        const string code = """
            namespace System
            {
                static class Console
                {
                    public static void Clear() { }
                }
            }

            class Program
            {
                static void Main() { global::System.Console.Clear(); }
            }
            """;
        var (tree, semanticModel) =
            CancellationInstrumentationTests.CreateSemanticModel(code);

        var rewritten = new ConsoleClearRewriter(semanticModel)
            .Visit(tree.GetRoot(TestContext.Current.CancellationToken))!;

        Assert.Contains(
            "global::System.Console.Clear()",
            rewritten.ToFullString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TextBoxConsole.StaticConsole.Clear",
            rewritten.ToFullString(),
            StringComparison.Ordinal);
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Localization.Interfaces;
using NAudio.Wave;

namespace KID.Services.CodeExecution
{
    public class CSharpCompiler : ICodeCompiler
    {
        private readonly ILocalizationService _localizationService;

        public CSharpCompiler(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public async Task<CompilationResult> CompileAsync(string code, CancellationToken cancellationToken = default)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            
            return await Task.Run(() =>
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                
                // Применяем реврайтер для замены Console.Clear()
                var rewriter = new ConsoleClearRewriter();
                var root = syntaxTree.GetRoot();
                var rewrittenRoot = rewriter.Visit(root);
                var rewrittenTree = syntaxTree.WithRootAndOptions(rewrittenRoot, syntaxTree.Options);

                var references = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location))
                    .ToList();

                // Явно добавляем ссылку на NAudio, чтобы пользовательские скрипты могли компилировать
                // выражения/сигнатуры, использующие NAudio.Wave.PlaybackState (например SoundState() / SoundPlayer.State).
                try
                {
                    var naudioPath = typeof(PlaybackState).Assembly.Location;
                    if (!string.IsNullOrEmpty(naudioPath))
                    {
                        var alreadyAdded = references
                            .OfType<PortableExecutableReference>()
                            .Any(r => string.Equals(r.FilePath, naudioPath, StringComparison.OrdinalIgnoreCase));

                        if (!alreadyAdded)
                        {
                            references.Add(MetadataReference.CreateFromFile(naudioPath));
                        }
                    }
                }
                catch { }

                var compilation = CSharpCompilation.Create(
                    "UserProgram",
                    new[] { rewrittenTree },
                    references,
                    new CSharpCompilationOptions(OutputKind.ConsoleApplication));

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var errors = result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(diagnostic =>
                        {
                            var lineSpan = diagnostic.Location.GetLineSpan();
                            int line = lineSpan.StartLinePosition.Line + 1;
                            string msg = diagnostic.GetMessage();
                            return _localizationService.GetString("Error_Compilation", line, msg);
                        }).ToList();

                    return new CompilationResult { Success = false, Errors = errors };
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                return new CompilationResult { Success = true, Assembly = assembly };
            }, cancellationToken);
        }

        // Внутренний класс для замены Console.Clear() на TextBoxConsole.StaticConsole.Clear()
        private class ConsoleClearRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Проверяем, является ли это вызовом Console.Clear() или System.Console.Clear()
                if (node.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.ValueText == "Clear" &&
                    node.ArgumentList.Arguments.Count == 0)
                {
                    bool isConsoleClear = false;
                    
                    // Случай 1: Console.Clear() - когда есть using System;
                    if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                        identifier.Identifier.ValueText == "Console")
                    {
                        isConsoleClear = true;
                    }
                    // Случай 2: System.Console.Clear()
                    else if (memberAccess.Expression is MemberAccessExpressionSyntax systemConsole &&
                             systemConsole.Expression is IdentifierNameSyntax systemIdentifier &&
                             systemIdentifier.Identifier.ValueText == "System" &&
                             systemConsole.Name.Identifier.ValueText == "Console")
                    {
                        isConsoleClear = true;
                    }
                    
                    if (isConsoleClear)
                    {
                        // Заменяем на KID.Services.CodeExecution.TextBoxConsole.StaticConsole.Clear()
                        // Строим цепочку пошагово: KID -> KID.Services -> KID.Services.CodeExecution -> ...
                        var kid = SyntaxFactory.IdentifierName("KID");
                        var kidServices = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            kid,
                            SyntaxFactory.IdentifierName("Services")
                        );
                        var kidServicesCodeExecution = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            kidServices,
                            SyntaxFactory.IdentifierName("CodeExecution")
                        );
                        var kidServicesCodeExecutionTextBoxConsole = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            kidServicesCodeExecution,
                            SyntaxFactory.IdentifierName("TextBoxConsole")
                        );
                        var kidServicesCodeExecutionTextBoxConsoleStaticConsole = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            kidServicesCodeExecutionTextBoxConsole,
                            SyntaxFactory.IdentifierName("StaticConsole")
                        );
                        var newExpression = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            kidServicesCodeExecutionTextBoxConsoleStaticConsole,
                            SyntaxFactory.IdentifierName("Clear")
                        );
                        
                        return SyntaxFactory.InvocationExpression(
                            newExpression,
                            SyntaxFactory.ArgumentList()
                        );
                    }
                }

                return base.VisitInvocationExpression(node);
            }
        }
    }
}
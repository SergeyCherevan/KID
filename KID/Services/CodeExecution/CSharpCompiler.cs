using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.CodeExecution.Interfaces;

namespace KID.Services.CodeExecution
{
    public class CSharpCompiler : ICodeCompiler
    {
        public async Task<CompilationResult> CompileAsync(string code, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);

                var references = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location));

                var compilation = CSharpCompilation.Create(
                    "UserProgram",
                    new[] { syntaxTree },
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
                            return $"Ошибка на строке {line}: {msg}";
                        }).ToList();

                    return new CompilationResult { Success = false, Errors = errors };
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                return new CompilationResult { Success = true, Assembly = assembly };
            }, cancellationToken);
        }
    }
}
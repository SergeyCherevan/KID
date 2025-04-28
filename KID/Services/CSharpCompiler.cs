using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using KID.Services.Interfaces;

namespace KID.Services
{
    public class CSharpCompiler : ICodeCompiler
    {
        public CompilationResult Compile(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>();

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
                        int lineNumber = lineSpan.StartLinePosition.Line + 1;
                        string message = diagnostic.GetMessage();
                        return $"Ошибка на строке {lineNumber}: {message}";
                    })
                    .ToList();

                return new CompilationResult
                {
                    Success = false,
                    Errors = errors
                };
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            return new CompilationResult
            {
                Success = true,
                Assembly = assembly
            };
        }
    }
}

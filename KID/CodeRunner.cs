using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KID
{
    public class CodeRunner
    {
        private TextWriter originalConsoleOut;
        private Action<string> consoleOutputCallback;

        public CodeRunner(Action<string> consoleOutputCallback)
        {
            this.consoleOutputCallback = consoleOutputCallback;
        }

        public bool CompileAndRun(string code)
        {
            // Перенаправляем вывод консоли
            originalConsoleOut = Console.Out;
            Console.SetOut(new ConsoleOutputWriter(consoleOutputCallback));

            try
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
                        .Select(d => d.ToString());

                    foreach (var error in errors)
                    {
                        consoleOutputCallback(error);
                    }

                    return false;
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                var entry = assembly.EntryPoint;
                if (entry != null)
                {
                    var parameters = entry.GetParameters().Length == 0 ? null : new object[] { new string[0] };
                    entry.Invoke(null, parameters);
                }

                return true;
            }
            catch (Exception ex)
            {
                consoleOutputCallback($"Ошибка: {ex.Message}");
                return false;
            }
            finally
            {
                // Восстанавливаем консоль
                Console.SetOut(originalConsoleOut);
            }
        }
    }

    public class ConsoleOutputWriter : TextWriter
    {
        private readonly Action<string> output;

        public ConsoleOutputWriter(Action<string> output)
        {
            this.output = output;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value)
        {
            output(value);
        }

        public override void Write(char value)
        {
            output(value.ToString());
        }
    }
}

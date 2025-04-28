using System;
using System.IO;
using System.Windows.Controls;
using KID.Services.Interfaces;

namespace KID.Services
{
    public class CodeExecutionService
    {
        private readonly ICodeCompiler compiler;
        private readonly ICodeRunner runner;

        public CodeExecutionService(ICodeCompiler compiler, ICodeRunner runner)
        {
            this.compiler = compiler;
            this.runner = runner;
        }

        public void Execute(string code, Action<string> consoleOutputCallback, Canvas graphicsCanvas)
        {
            var compilationResult = compiler.Compile(code);

            if (compilationResult.Success)
            {
                Graphics.Init(graphicsCanvas);

                var originalConsoleOut = Console.Out;
                Console.SetOut(new ConsoleRedirector(consoleOutputCallback));

                try
                {
                    runner.Run(compilationResult.Assembly);
                }
                catch (Exception ex)
                {
                    consoleOutputCallback?.Invoke($"Ошибка выполнения: {ex.Message}");
                }
                finally
                {
                    Console.SetOut(originalConsoleOut);
                }
            }
            else
            {
                foreach (var error in compilationResult.Errors)
                {
                    consoleOutputCallback?.Invoke(error);
                }
            }
        }
    }
}

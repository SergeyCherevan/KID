using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using KID.Services.Interfaces;

namespace KID.Services
{
    public class CodeExecutionService
    {
        private readonly ICodeCompiler compiler;
        private readonly ICodeRunner runner;
        private bool isRunning;

        public CodeExecutionService(ICodeCompiler compiler, ICodeRunner runner)
        {
            this.compiler = compiler;
            this.runner = runner;
        }

        public async Task ExecuteAsync(string code, Action<string> consoleOutputCallback, Canvas graphicsCanvas, CancellationToken token = default)
        {
            if (isRunning) return;
            isRunning = true;

            try
            {
                var result = await compiler.CompileAsync(code, token);

                if (!result.Success)
                {
                    foreach (var error in result.Errors)
                        consoleOutputCallback?.Invoke(error);
                    return;
                }

                Graphics.Init(graphicsCanvas);

                var originalConsole = Console.Out;
                Console.SetOut(new ConsoleRedirector(consoleOutputCallback));

                try
                {
                    await Task.Run(async () => 
                    {
                        await runner.RunAsync(result.Assembly, token);
                    }, token);
                }
                catch (Exception ex)
                {
                    consoleOutputCallback?.Invoke($"Ошибка выполнения: {ex.Message}");
                }
                finally
                {
                    Console.SetOut(originalConsole);
                }
            }
            finally
            {
                isRunning = false;
            }
        }
    }
}
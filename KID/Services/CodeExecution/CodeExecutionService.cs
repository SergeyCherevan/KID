using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using KID.Services.CodeExecution.Interfaces;

namespace KID.Services.CodeExecution
{
    public class CodeExecutionService : ICodeExecutionService
    {
        private readonly ICodeCompiler compiler;
        private readonly ICodeRunner runner;
        private bool isRunning;

        public CodeExecutionService(ICodeCompiler compiler, ICodeRunner runner)
        {
            this.compiler = compiler;
            this.runner = runner;
        }

        public async Task ExecuteAsync(string code, CodeExecutionContext context)
        {
            if (isRunning) return;
            isRunning = true;

            try
            {
                var result = await compiler.CompileAsync(code, context.CancellationToken);

                if (!result.Success)
                {
                    foreach (var error in result.Errors)
                        context.ConsoleOutputCallback?.Invoke(error);
                    return;
                }

                Graphics.Init(context.GraphicsCanvas);

                var originalConsole = Console.Out;
                Console.SetOut(new ConsoleRedirector(context.ConsoleOutputCallback));

                try
                {
                    await Task.Run(async () => 
                    {
                        await runner.RunAsync(result.Assembly, context.CancellationToken);
                    }, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    context.ConsoleOutputCallback?.Invoke($"Ошибка выполнения: {ex.Message}");
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
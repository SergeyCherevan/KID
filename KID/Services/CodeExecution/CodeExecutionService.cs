using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using KID.Services.CodeExecution.Contexts.Interfaces;
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

        public async Task ExecuteAsync(string code, ICodeExecutionContext context)
        {
            if (isRunning) return;
            isRunning = true;

            try
            {
                context.Init();

                var result = await compiler.CompileAsync(code, context.CancellationToken);

                if (!result.Success)
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine(error);
                    }
                    return;
                }

                try
                {
                    await runner.RunAsync(result.Assembly, context.CancellationToken);
                }
                catch
                {
                    // Ошибки выполнения обрабатываются в DefaultCodeRunner через Console
                    // Здесь просто пробрасываем исключение дальше
                    throw;
                }
                finally
                {
                    context.Dispose();
                }
            }
            finally
            {
                isRunning = false;
            }
        }
    }
}
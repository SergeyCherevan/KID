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
            this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public async Task ExecuteAsync(string code, ICodeExecutionContext context)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            
            if (isRunning) return;
            isRunning = true;

            try
            {
                context.Init();

                var result = await compiler.CompileAsync(code, context.CancellationToken);
                
                if (result == null)
                    throw new InvalidOperationException("Compilation result is null");

                if (!result.Success)
                {
                    if (result.Errors != null)
                    {
                        foreach (var error in result.Errors)
                        {
                            if (error != null)
                                Console.WriteLine(error);
                        }
                    }
                    return;
                }

                if (result.Assembly == null)
                    throw new InvalidOperationException("Compiled assembly is null");

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
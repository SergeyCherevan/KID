using System;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.Interfaces;

namespace KID.Services
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

        public async Task ExecuteAsync(string code, IExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (isRunning) return;
            isRunning = true;

            try
            {
                var result = await compiler.CompileAsync(code, context.CancellationToken);

                if (!result.Success)
                {
                    foreach (var error in result.Errors)
                        context.ReportError(error);
                    return;
                }

                // Инициализация графики уже выполнена в ExecutionContextFactory.Create()

                try
                {
                    await Task.Run(async () => 
                    {
                        await runner.RunAsync(result.Assembly, context);
                    }, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    context.ReportError($"Ошибка выполнения: {ex.Message}");
                }
            }
            finally
            {
                isRunning = false;
            }
        }
    }
}
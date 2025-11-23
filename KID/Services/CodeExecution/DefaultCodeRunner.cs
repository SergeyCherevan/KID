using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.CodeExecution.Interfaces;

namespace KID.Services.CodeExecution
{
    public class DefaultCodeRunner : ICodeRunner
    {
        public async Task RunAsync(Assembly assembly, CancellationToken cancellationToken = default)
        {
            CancellationManager.CurrentToken = cancellationToken;

            await Task.Run(() =>
            {
                var entry = assembly.EntryPoint;
                if (entry != null)
                {
                    var parameters = entry.GetParameters().Length == 0 ? null : new object[] { new string[0] };
                    try
                    {
                        entry.Invoke(null, parameters);
                    }
                    catch (TargetInvocationException ex)
                    {
                        // Извлекаем внутреннее исключение
                        if (ex.InnerException is OperationCanceledException)
                        {
                            Console.WriteLine("Программа остановлена");
                        }
                        else
                        {
                            var innerEx = ex.InnerException;
                            var errorMessage = innerEx?.Message ?? ex.Message;
                            var stackTrace = innerEx?.StackTrace ?? ex.StackTrace;
                            Console.WriteLine($"Ошибка выполнения: {errorMessage}\nСтек: {stackTrace}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Программа остановлена");
                    }
                }
            },
            cancellationToken);
        }
    }
}
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.Interfaces;

namespace KID.Services
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
                            // Пробрасываем другие исключения
                            throw ex.InnerException ?? ex;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Программа остановленна");
                    }
                }
            }, cancellationToken);
        }
    }
}
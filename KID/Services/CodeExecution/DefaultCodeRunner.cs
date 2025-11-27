using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Services.CodeExecution
{
    public class DefaultCodeRunner : ICodeRunner
    {
        private readonly ILocalizationService _localizationService;

        public DefaultCodeRunner(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

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
                            Console.WriteLine(_localizationService.GetString("Notification_ProgramStopped"));
                        }
                        else
                        {
                            var innerEx = ex.InnerException;
                            var errorMessage = innerEx?.Message ?? ex.Message;
                            var stackTrace = innerEx?.StackTrace ?? ex.StackTrace;
                            Console.WriteLine(_localizationService.GetString("Error_Execution", errorMessage));
                            if (!string.IsNullOrEmpty(stackTrace))
                            {
                                Console.WriteLine(_localizationService.GetString("Error_StackTrace", stackTrace));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine(_localizationService.GetString("Notification_ProgramStopped"));
                    }
                    finally
                    {
                        Console.WriteLine(_localizationService.GetString("Notification_ProgramFinished"));
                    }
                }
            },
            cancellationToken);
        }
    }
}
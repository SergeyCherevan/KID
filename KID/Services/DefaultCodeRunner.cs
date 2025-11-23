using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.Interfaces;

namespace KID.Services
{
    public class DefaultCodeRunner : ICodeRunner
    {
        public async Task RunAsync(Assembly assembly, IExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            CancellationManager.CurrentToken = context.CancellationToken;

            await Task.Run(() =>
            {
                var entry = assembly.EntryPoint;
                if (entry != null)
                {
                    var parameters = entry.GetParameters().Length == 0 
                        ? null 
                        : new object[] { new string[0] };
                    
                    try 
                    {
                        entry.Invoke(null, parameters);
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.InnerException is OperationCanceledException)
                        {
                            context.Console.WriteLine("Программа остановлена");
                        }
                        else
                        {
                            context.ReportError($"Ошибка: {ex.InnerException?.Message ?? ex.Message}");
                            throw ex.InnerException ?? ex;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        context.Console.WriteLine("Программа остановлена");
                    }
                }
            }, context.CancellationToken);
        }
    }
}
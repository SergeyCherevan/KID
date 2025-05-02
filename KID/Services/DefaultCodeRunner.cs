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
                    catch (OperationCanceledException)
                    {
                        // Обработка отмены
                    }
                }
            }, cancellationToken);
        }
    }
}
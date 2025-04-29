using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KID.Services.Interfaces;

namespace KID.Services
{
    public class TaskCodeRunner : ICodeRunner
    {
        public async Task RunAsync(CompilationResult compilationResult, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var entry = compilationResult.Assembly.EntryPoint;
                if (entry != null)
                {
                    var parameters = entry.GetParameters().Length == 0 ? null : new object[] { new string[0] };
                    entry.Invoke(null, parameters);
                }
            }, cancellationToken);
        }
    }
}
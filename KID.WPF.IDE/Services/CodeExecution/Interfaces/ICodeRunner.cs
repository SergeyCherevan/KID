using System.Reflection;

namespace KID.Services.CodeExecution.Interfaces
{
    public interface ICodeRunner
    {
        Task RunAsync(Assembly assembly, CancellationToken cancellationToken = default);
    }
}

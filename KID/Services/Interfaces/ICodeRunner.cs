using System.Reflection;

namespace KID.Services.Interfaces
{
    public interface ICodeRunner
    {
        Task RunAsync(Assembly assembly, CancellationToken cancellationToken = default);
    }
}

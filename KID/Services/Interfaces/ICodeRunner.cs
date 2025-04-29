using System.Reflection;

namespace KID.Services.Interfaces
{
    public interface ICodeRunner
    {
        Task RunAsync(CompilationResult compilationResult, CancellationToken cancellationToken = default);
    }
}

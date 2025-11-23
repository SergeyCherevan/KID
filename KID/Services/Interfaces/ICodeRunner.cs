using System.Reflection;
using System.Threading.Tasks;

namespace KID.Services.Interfaces
{
    public interface ICodeRunner
    {
        Task RunAsync(Assembly assembly, IExecutionContext context);
    }
}

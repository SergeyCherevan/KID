using System.Threading;
using System.Threading.Tasks;

namespace KID.Services.Interfaces
{
    public interface ICodeExecutionService
    {
        Task ExecuteAsync(string code, IExecutionContext context);
    }
}


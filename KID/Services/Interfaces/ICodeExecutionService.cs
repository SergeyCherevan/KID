using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.Services.Interfaces
{
    public interface ICodeExecutionService
    {
        Task ExecuteAsync(string code, Action<string> consoleOutputCallback, Canvas graphicsCanvas, CancellationToken token = default);
    }
}


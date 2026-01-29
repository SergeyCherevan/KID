using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface ICodeExecutionContext : IDisposable
    {
        IGraphicsContext GraphicsContext { get; set; }
        IConsoleContext ConsoleContext { get; set; }
        CancellationToken CancellationToken { get; set; }
        Dispatcher Dispatcher { get; set; }

        void Init();
    }
}

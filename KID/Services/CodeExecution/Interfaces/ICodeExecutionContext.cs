using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KID.Services.CodeExecution.Interfaces
{
    public interface ICodeExecutionContext : IDisposable
    {
        IGraphicsContext GraphicsContext { get; set; }
        IConsoleContext ConsoleContext { get; set; }
        CancellationToken CancellationToken { get; set; }

        void Init();
    }
}

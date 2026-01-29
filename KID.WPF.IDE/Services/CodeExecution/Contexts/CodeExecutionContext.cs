using KID.Services.CodeExecution.Contexts.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using KID;

namespace KID.Services.CodeExecution.Contexts
{
    public class CodeExecutionContext : ICodeExecutionContext
    {
        public IGraphicsContext GraphicsContext { get; set; }
        public IConsoleContext ConsoleContext { get; set; }
        public CancellationToken CancellationToken { get; set; } = default;
        public Dispatcher Dispatcher { get; set; }

        public void Init()
        {
            DispatcherManager.Init(Dispatcher);
            GraphicsContext?.Init();
            ConsoleContext?.Init();
        }

        public void Dispose()
        {
            GraphicsContext?.Dispose();
            ConsoleContext?.Dispose();
        }
    }
}

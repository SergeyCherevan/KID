using KID.Services.CodeExecution.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.Services.CodeExecution
{
    public class CodeExecutionContext : ICodeExecutionContext
    {
        public IGraphicsContext GraphicsContext {  get; set; }
        public IConsoleContext ConsoleContext { get; set; }
        public CancellationToken CancellationToken { get; set; } = default;

        public void Init()
        {
            GraphicsContext.Init();
            ConsoleContext.Init();
        }

        public void Dispose()
        {
            GraphicsContext.Dispose();
            ConsoleContext.Dispose();
        }
    }
}

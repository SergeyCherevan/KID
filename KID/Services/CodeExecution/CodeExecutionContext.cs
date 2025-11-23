using KID.Services.CodeExecution.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace KID.Services.CodeExecution
{
    public class CodeExecutionContext
    {
        public IGraphicsContext GraphicsContext {  get; set; }
        public IConsoleContext ConsoleContext { get; set; }
        public CancellationToken CancellationToken { get; set; } = default;
    }
}

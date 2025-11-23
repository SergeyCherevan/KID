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
        public Action<string> ConsoleOutputCallback { get; set; }
        public Canvas GraphicsCanvas {  get; set; }
        public CancellationToken CancellationToken { get; set; } = default;
    }
}

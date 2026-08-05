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
        /// <summary>
        /// Контекст графического вывода и связанных API пользовательской программы.
        /// </summary>
        IGraphicsContext GraphicsContext { get; set; }

        /// <summary>
        /// Контекст перенаправления стандартных потоков консоли.
        /// </summary>
        IConsoleContext ConsoleContext { get; set; }

        /// <summary>
        /// Токен остановки текущего запуска программы.
        /// </summary>
        CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Диспетчер UI-потока для безопасного доступа к WPF-контролам.
        /// </summary>
        Dispatcher Dispatcher { get; set; }

        void Init();
    }
}

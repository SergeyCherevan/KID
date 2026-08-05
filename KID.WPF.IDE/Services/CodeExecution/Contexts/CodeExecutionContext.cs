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
    /// <summary>
    /// Объединяет UI-контексты и состояние одного запуска пользовательской программы.
    /// </summary>
    public class CodeExecutionContext : ICodeExecutionContext
    {
        /// <summary>
        /// Подготавливает Canvas и связанные графические, мышиные, клавиатурные и музыкальные API.
        /// Обязателен при создании контекста и инициализируется методом <see cref="Init"/>.
        /// </summary>
        public required IGraphicsContext GraphicsContext { get; set; }

        /// <summary>
        /// Перенаправляет стандартные потоки консоли в UI и восстанавливает их после выполнения.
        /// Обязателен при создании контекста и инициализируется методом <see cref="Init"/>.
        /// </summary>
        public required IConsoleContext ConsoleContext { get; set; }

        /// <summary>
        /// Передаёт запрос Stop компилятору и выполняемой пользовательской программе.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = default;

        /// <summary>
        /// Диспетчер UI-потока, через который библиотека выполняет операции с WPF-контролами.
        /// Обязателен при создании контекста и регистрируется методом <see cref="Init"/>.
        /// </summary>
        public required Dispatcher Dispatcher { get; set; }

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

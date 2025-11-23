using System.Threading;

namespace KID.Services.Interfaces
{
    /// <summary>
    /// Контекст выполнения кода, содержащий все необходимые зависимости
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Графический контекст для инициализации графики
        /// </summary>
        IGraphicsContext Graphics { get; }
        
        /// <summary>
        /// Консольный вывод и ввод
        /// </summary>
        IConsole Console { get; }
        
        /// <summary>
        /// Токен отмены операции
        /// </summary>
        CancellationToken CancellationToken { get; }
        
        /// <summary>
        /// Обработчик ошибок выполнения
        /// </summary>
        void ReportError(string message);
        
        /// <summary>
        /// Устанавливает цель для графики (например, Canvas)
        /// </summary>
        void SetGraphicsTarget(object graphicsTarget);
        
        /// <summary>
        /// Устанавливает цель для консоли (например, TextBox)
        /// </summary>
        void SetConsoleTarget(object consoleTarget);
    }
}


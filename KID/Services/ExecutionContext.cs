using System;
using System.Threading;
using KID.Services.Interfaces;

namespace KID.Services
{
    /// <summary>
    /// Реализация контекста выполнения кода
    /// </summary>
    public class ExecutionContext : IExecutionContext, IDisposable
    {
        public IGraphicsContext Graphics { get; }
        public IConsole Console { get; private set; }
        public CancellationToken CancellationToken { get; }
        
        private readonly IConsoleContext consoleContext;
        private object graphicsTarget;
        private IConsole originalConsole;

        public ExecutionContext(
            IGraphicsContext graphicsContext,
            IConsoleContext consoleContext,
            CancellationToken cancellationToken)
        {
            Graphics = graphicsContext ?? throw new ArgumentNullException(nameof(graphicsContext));
            this.consoleContext = consoleContext ?? throw new ArgumentNullException(nameof(consoleContext));
            CancellationToken = cancellationToken;
        }

        public void ReportError(string message)
        {
            Console?.WriteLine(message);
        }

        public void SetGraphicsTarget(object graphicsTarget)
        {
            this.graphicsTarget = graphicsTarget;
            if (graphicsTarget != null)
            {
                Graphics.Initialize(graphicsTarget);
            }
        }

        public void SetConsoleTarget(object consoleTarget)
        {
            // Освобождаем предыдущий консоль, если был
            if (Console is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            if (consoleTarget != null)
            {
                Console = consoleContext.CreateConsole(consoleTarget);
            }
        }

        public void Dispose()
        {
            if (Console is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}


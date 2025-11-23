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
        public IConsole Console { get; }
        public CancellationToken CancellationToken { get; }
        
        private readonly Action<string> errorHandler;
        private object graphicsTarget;

        public ExecutionContext(
            IGraphicsContext graphicsContext,
            IConsole console,
            CancellationToken cancellationToken,
            Action<string> errorHandler = null)
        {
            Graphics = graphicsContext ?? throw new ArgumentNullException(nameof(graphicsContext));
            Console = console ?? throw new ArgumentNullException(nameof(console));
            CancellationToken = cancellationToken;
            this.errorHandler = errorHandler;
        }

        public void ReportError(string message)
        {
            errorHandler?.Invoke(message);
        }

        public void SetGraphicsTarget(object graphicsTarget)
        {
            this.graphicsTarget = graphicsTarget;
            if (graphicsTarget != null)
            {
                Graphics.Initialize(graphicsTarget);
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


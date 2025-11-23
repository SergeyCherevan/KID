using System;
using System.Threading;
using System.Windows.Controls;
using KID.Services.Interfaces;

namespace KID.Services
{
    /// <summary>
    /// Фабрика для создания контекста выполнения
    /// </summary>
    public class ExecutionContextFactory
    {
        private readonly IGraphicsContext graphicsContext;
        private readonly IConsoleContext consoleContext;

        public ExecutionContextFactory(
            IGraphicsContext graphicsContext,
            IConsoleContext consoleContext)
        {
            this.graphicsContext = graphicsContext ?? throw new ArgumentNullException(nameof(graphicsContext));
            this.consoleContext = consoleContext ?? throw new ArgumentNullException(nameof(consoleContext));
        }

        public IExecutionContext Create(
            Canvas graphicsCanvas,
            TextBox consoleTextBox,
            CancellationToken cancellationToken = default)
        {
            var context = new ExecutionContext(
                graphicsContext,
                consoleContext,
                cancellationToken
            );
            
            context.SetGraphicsTarget(graphicsCanvas);
            context.SetConsoleTarget(consoleTextBox);
            
            return context;
        }
    }
}


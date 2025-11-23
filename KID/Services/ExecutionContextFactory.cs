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

        public ExecutionContextFactory(IGraphicsContext graphicsContext)
        {
            this.graphicsContext = graphicsContext ?? throw new ArgumentNullException(nameof(graphicsContext));
        }

        public IExecutionContext Create(
            Canvas graphicsCanvas,
            Action<string> consoleOutput = null,
            Action<string> errorOutput = null,
            Func<string> inputProvider = null, // Для поддержки ReadLine
            Func<ConsoleKeyInfo> keyProvider = null, // Для поддержки ReadKey
            CancellationToken cancellationToken = default)
        {
            var console = new WpfConsole(
                outputCallback: consoleOutput,
                inputProvider: inputProvider,
                keyProvider: keyProvider
            );
            
            var context = new ExecutionContext(
                graphicsContext,
                console,
                cancellationToken,
                errorOutput ?? consoleOutput
            );
            
            context.SetGraphicsTarget(graphicsCanvas);
            
            return context;
        }
    }
}


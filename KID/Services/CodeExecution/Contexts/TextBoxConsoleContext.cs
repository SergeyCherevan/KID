using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.Interfaces;
using System;
using System.IO;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts
{
    public class TextBoxConsoleContext : IConsoleContext
    {
        private TextWriter originalConsoleOut;
        private TextReader originalConsoleIn;
        private TextWriter originalConsoleError;
        private TextBoxConsole? textBoxConsole;
        public object ConsoleTarget { get; set; }

        public TextBoxConsoleContext(TextBox textBox)
        {
            ConsoleTarget = textBox ?? throw new ArgumentNullException(nameof(textBox));
        }

        public void Init()
        {
            if (ConsoleTarget is not TextBox textBox)
                throw new InvalidOperationException("ConsoleTarget must be a TextBox");

            // Сохраняем оригинальные потоки
            originalConsoleOut = Console.Out;
            originalConsoleIn = Console.In;
            originalConsoleError = Console.Error;

            // Создаем экземпляр TextBoxConsole
            textBoxConsole = new TextBoxConsole(textBox);

            // Перенаправляем потоки Console
            if (textBoxConsole != null)
            {
                Console.SetOut(textBoxConsole.Out);
                Console.SetIn(textBoxConsole.In);
                Console.SetError(textBoxConsole.Error);
            }
        }

        public void Dispose()
        {
            // Восстанавливаем оригинальные потоки
            if (originalConsoleOut != null)
                Console.SetOut(originalConsoleOut);
            if (originalConsoleIn != null)
                Console.SetIn(originalConsoleIn);
            if (originalConsoleError != null)
                Console.SetError(originalConsoleError);

            textBoxConsole = null;
        }
    }
}

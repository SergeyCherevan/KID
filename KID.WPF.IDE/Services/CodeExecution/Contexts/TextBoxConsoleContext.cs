using KID.Services.CodeExecution.Contexts.Interfaces;
using KID.Services.Interfaces;
using System;
using System.IO;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts
{
    public class TextBoxConsoleContext : IConsoleContext
    {
        /*
         * Исходные глобальные потоки System.Console. Метод Init сохраняет их перед перенаправлением,
         * а Dispose использует для восстановления. До Init поля остаются null, поэтому Dispose
         * проверяет каждый поток отдельно и безопасен даже при неполной инициализации контекста.
         */
        private TextWriter? originalConsoleOut;
        private TextReader? originalConsoleIn;
        private TextWriter? originalConsoleError;

        /*
         * Адаптер между глобальными потоками System.Console и WPF TextBox. Создаётся в Init
         * для перенаправления ввода и вывода; ссылка очищается в Dispose после восстановления потоков.
         */
        private TextBoxConsole? textBoxConsole;

        /// <summary>
        /// Целевой WPF-контрол, в который перенаправляются стандартные потоки консоли.
        /// Задаётся конструктором и используется методом <see cref="Init"/>.
        /// </summary>
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

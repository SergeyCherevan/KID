using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using KID.Services.Interfaces;

namespace KID.Services
{
    /// <summary>
    /// Реализация IConsole для WPF приложения
    /// </summary>
    public class WpfConsole : IConsole, IDisposable
    {
        private readonly Action<string> outputCallback;
        private readonly Func<string> inputProvider; // Для ReadLine
        private readonly Func<ConsoleKeyInfo> keyProvider; // Для ReadKey
        private readonly Dispatcher dispatcher;
        
        private ConsoleColor foregroundColor = ConsoleColor.Gray;
        private ConsoleColor backgroundColor = ConsoleColor.Black;
        private int cursorLeft = 0;
        private int cursorTop = 0;
        private int windowWidth = 120;
        private int windowHeight = 30;
        private int bufferWidth = 120;
        private int bufferHeight = 300;
        
        private TextWriter originalOut;
        private TextReader originalIn;
        private TextWriter originalError;
        
        private ConsoleTextWriter consoleOut;
        private ConsoleTextReader consoleIn;
        private ConsoleTextWriter consoleError;
        
        public event EventHandler<string> OutputReceived;

        public WpfConsole(
            Action<string> outputCallback,
            Func<string> inputProvider = null,
            Func<ConsoleKeyInfo> keyProvider = null)
        {
            this.outputCallback = outputCallback ?? throw new ArgumentNullException(nameof(outputCallback));
            this.inputProvider = inputProvider;
            this.keyProvider = keyProvider;
            this.dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            
            // Сохраняем оригинальные потоки
            originalOut = System.Console.Out;
            originalIn = System.Console.In;
            originalError = System.Console.Error;
            
            // Создаем наши потоки
            consoleOut = new ConsoleTextWriter(this);
            consoleIn = new ConsoleTextReader(this);
            consoleError = new ConsoleTextWriter(this);
            
            Out = consoleOut;
            In = consoleIn;
            Error = consoleError;
            
            // Перенаправляем Console
            System.Console.SetOut(Out);
            System.Console.SetIn(In);
            System.Console.SetError(Error);
        }

        // === Вывод ===
        public void Write(string value)
        {
            if (dispatcher.CheckAccess())
            {
                outputCallback(value);
                OutputReceived?.Invoke(this, value);
            }
            else
            {
                dispatcher.BeginInvoke(() => 
                {
                    outputCallback(value);
                    OutputReceived?.Invoke(this, value);
                }, DispatcherPriority.Background);
            }
        }

        public void WriteLine(string value)
        {
            Write(value + Environment.NewLine);
        }

        public void WriteLine()
        {
            Write(Environment.NewLine);
        }

        // === Ввод ===
        public int Read()
        {
            if (inputProvider == null)
                throw new NotSupportedException("Ввод не поддерживается. Укажите inputProvider при создании WpfConsole.");
            
            var input = inputProvider();
            return input.Length > 0 ? input[0] : -1;
        }

        public string ReadLine()
        {
            if (inputProvider == null)
                throw new NotSupportedException("ReadLine не поддерживается. Укажите inputProvider при создании WpfConsole.");
            
            return inputProvider();
        }

        public ConsoleKeyInfo ReadKey()
        {
            return ReadKey(false);
        }

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            if (keyProvider == null)
                throw new NotSupportedException("ReadKey не поддерживается. Укажите keyProvider при создании WpfConsole.");
            
            return keyProvider();
        }

        // === Потоки ===
        public TextWriter Out { get; set; }
        public TextReader In { get; set; }
        public TextWriter Error { get; set; }

        // === Цвета ===
        public ConsoleColor ForegroundColor
        {
            get => foregroundColor;
            set => foregroundColor = value;
        }

        public ConsoleColor BackgroundColor
        {
            get => backgroundColor;
            set => backgroundColor = value;
        }

        public void ResetColor()
        {
            foregroundColor = ConsoleColor.Gray;
            backgroundColor = ConsoleColor.Black;
        }

        // === Позиция курсора ===
        public int CursorLeft
        {
            get => cursorLeft;
            set => cursorLeft = Math.Max(0, value);
        }

        public int CursorTop
        {
            get => cursorTop;
            set => cursorTop = Math.Max(0, value);
        }

        public void SetCursorPosition(int left, int top)
        {
            CursorLeft = left;
            CursorTop = top;
        }

        // === Размеры окна ===
        public int WindowWidth
        {
            get => windowWidth;
            set => windowWidth = Math.Max(1, value);
        }

        public int WindowHeight
        {
            get => windowHeight;
            set => windowHeight = Math.Max(1, value);
        }

        public int BufferWidth
        {
            get => bufferWidth;
            set => bufferWidth = Math.Max(1, value);
        }

        public int BufferHeight
        {
            get => bufferHeight;
            set => bufferHeight = Math.Max(1, value);
        }

        // === Другие методы ===
        public void Clear()
        {
            // Можно добавить событие для очистки UI
            Write("\f"); // Form feed - символ очистки, или специальная команда
        }

        public void Beep()
        {
            System.Console.Beep();
        }

        public void Beep(int frequency, int duration)
        {
            System.Console.Beep(frequency, duration);
        }

        public void Dispose()
        {
            System.Console.SetOut(originalOut);
            System.Console.SetIn(originalIn);
            System.Console.SetError(originalError);
        }
    }

    // Вспомогательный класс для TextWriter
    internal class ConsoleTextWriter : TextWriter
    {
        private readonly WpfConsole console;

        public ConsoleTextWriter(WpfConsole console)
        {
            this.console = console;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string value)
        {
            console.Write(value);
        }

        public override void Write(char value)
        {
            console.Write(value.ToString());
        }
    }

    // Вспомогательный класс для TextReader
    internal class ConsoleTextReader : TextReader
    {
        private readonly WpfConsole console;

        public ConsoleTextReader(WpfConsole console)
        {
            this.console = console;
        }

        public override int Read()
        {
            return console.Read();
        }

        public override string ReadLine()
        {
            return console.ReadLine();
        }
    }
}


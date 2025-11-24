using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KID.Services.Interfaces;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Реализация IConsole для WPF TextBox
    /// </summary>
    public class TextBoxConsole : IConsole
    {
        private readonly TextBox textBox;
        private readonly Dispatcher dispatcher;
        private readonly ConcurrentQueue<string> inputQueue;
        private readonly AutoResetEvent inputAvailable;
        private TextWriter textWriter;
        private TextReader textReader;

        public TextBoxConsole(TextBox textBox)
        {
            this.textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            dispatcher = Application.Current.Dispatcher;
            inputQueue = new ConcurrentQueue<string>();
            inputAvailable = new AutoResetEvent(false);
            
            // Инициализация потоков
            textWriter = new TextBoxTextWriter(this);
            textReader = new TextBoxTextReader(this);
            
            // Настройка TextBox для ввода
            textBox.KeyDown += TextBox_KeyDown;
        }

        // === Вывод ===
        public void Write(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            
            InvokeOnUIThread(() =>
            {
                textBox.AppendText(value);
                textBox.ScrollToEnd();
                OutputReceived?.Invoke(this, value);
            });
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
            string line = ReadLine();
            if (string.IsNullOrEmpty(line)) return -1;
            return line.Length > 0 ? line[0] : -1;
        }

        public string ReadLine()
        {
            // Ожидаем ввода
            inputAvailable.WaitOne();
            
            if (inputQueue.TryDequeue(out string? result))
            {
                return result ?? string.Empty;
            }
            
            return string.Empty;
        }

        public ConsoleKeyInfo ReadKey()
        {
            return ReadKey(false);
        }

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            // Упрощенная реализация - возвращаем первую клавишу из очереди
            inputAvailable.WaitOne();
            
            if (inputQueue.TryDequeue(out string? keyStr) && keyStr != null && keyStr.Length > 0)
            {
                char keyChar = keyStr[0];
                ConsoleKey key = ConvertCharToConsoleKey(keyChar);
                return new ConsoleKeyInfo(keyChar, key, false, false, false);
            }
            
            return new ConsoleKeyInfo('\0', ConsoleKey.NoName, false, false, false);
        }

        // === Потоки ===
        public TextWriter Out
        {
            get => textWriter;
            set => textWriter = value ?? throw new ArgumentNullException(nameof(value));
        }

        public TextReader In
        {
            get => textReader;
            set => textReader = value ?? throw new ArgumentNullException(nameof(value));
        }

        public TextWriter Error
        {
            get => textWriter; // Используем тот же поток для ошибок
            set => textWriter = value ?? throw new ArgumentNullException(nameof(value));
        }

        // === События ===
        public event EventHandler<string>? OutputReceived;

        // === Вспомогательные методы ===
        private void InvokeOnUIThread(Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            }
        }

        private ConsoleKey ConvertCharToConsoleKey(char c)
        {
            if (char.IsLetter(c))
            {
                return (ConsoleKey)char.ToUpper(c);
            }
            if (char.IsDigit(c))
            {
                return (ConsoleKey)((int)ConsoleKey.D0 + (c - '0'));
            }
            
            return c switch
            {
                ' ' => ConsoleKey.Spacebar,
                '\r' => ConsoleKey.Enter,
                '\t' => ConsoleKey.Tab,
                '\b' => ConsoleKey.Backspace,
                _ => ConsoleKey.NoName
            };
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // Получаем текущую строку от последнего перевода строки до курсора
                int caretIndex = textBox.CaretIndex;
                string text = textBox.Text;
                
                int lineStart = 0;
                for (int i = caretIndex - 1; i >= 0; i--)
                {
                    if (text[i] == '\n')
                    {
                        lineStart = i + 1;
                        break;
                    }
                }
                
                int lineLength = caretIndex - lineStart;
                string line = lineLength > 0 ? text.Substring(lineStart, lineLength) : string.Empty;
                
                inputQueue.Enqueue(line);
                inputAvailable.Set();
            }
        }

        // === Внутренние классы для потоков ===
        private class TextBoxTextWriter : TextWriter
        {
            private readonly TextBoxConsole console;

            public TextBoxTextWriter(TextBoxConsole console)
            {
                this.console = console;
            }

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(string? value)
            {
                if (value != null)
                {
                    console.Write(value);
                }
            }

            public override void Write(char value)
            {
                console.Write(value.ToString());
            }
        }

        private class TextBoxTextReader : TextReader
        {
            private readonly TextBoxConsole console;

            public TextBoxTextReader(TextBoxConsole console)
            {
                this.console = console;
            }

            public override int Read()
            {
                return console.Read();
            }

            public override string? ReadLine()
            {
                return console.ReadLine();
            }
        }
    }
}


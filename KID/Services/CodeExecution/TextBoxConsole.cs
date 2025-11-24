using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        private ConsoleColor foregroundColor;
        private ConsoleColor backgroundColor;
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
            textBox.TextChanged += TextBox_TextChanged;
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

        // === Цвета ===
        public ConsoleColor ForegroundColor
        {
            get => foregroundColor;
            set
            {
                foregroundColor = value;
                InvokeOnUIThread(() =>
                {
                    textBox.Foreground = new SolidColorBrush(ConvertConsoleColorToColor(value));
                });
            }
        }

        public ConsoleColor BackgroundColor
        {
            get => backgroundColor;
            set
            {
                backgroundColor = value;
                InvokeOnUIThread(() =>
                {
                    textBox.Background = new SolidColorBrush(ConvertConsoleColorToColor(value));
                });
            }
        }

        public void ResetColor()
        {
            ForegroundColor = ConsoleColor.Gray;
            BackgroundColor = ConsoleColor.Black;
        }

        // === Позиция курсора ===
        public int CursorLeft
        {
            get => InvokeOnUIThread(() => GetCursorPosition().left);
            set => InvokeOnUIThread(() => SetCursorPosition(value, CursorTop));
        }

        public int CursorTop
        {
            get => InvokeOnUIThread(() => GetCursorPosition().top);
            set => InvokeOnUIThread(() => SetCursorPosition(CursorLeft, value));
        }

        public void SetCursorPosition(int left, int top)
        {
            InvokeOnUIThread(() =>
            {
                // В WPF TextBox нет прямого аналога, используем CaretIndex
                int position = Math.Min(left, textBox.Text.Length);
                textBox.CaretIndex = position;
                textBox.Focus();
            });
        }

        // === Размеры окна ===
        public int WindowWidth
        {
            get => InvokeOnUIThread(() => (int)textBox.ActualWidth);
            set { /* WPF TextBox не поддерживает установку ширины напрямую */ }
        }

        public int WindowHeight
        {
            get => InvokeOnUIThread(() => (int)textBox.ActualHeight);
            set { /* WPF TextBox не поддерживает установку высоты напрямую */ }
        }

        public int BufferWidth
        {
            get => WindowWidth;
            set => WindowWidth = value;
        }

        public int BufferHeight
        {
            get => WindowHeight;
            set => WindowHeight = value;
        }

        // === Другие методы ===
        public void Clear()
        {
            InvokeOnUIThread(() =>
            {
                textBox.Clear();
            });
        }

        public void Beep()
        {
            System.Console.Beep();
        }

        public void Beep(int frequency, int duration)
        {
            System.Console.Beep(frequency, duration);
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

        private T InvokeOnUIThread<T>(Func<T> func)
        {
            if (dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                T result = default(T)!;
                dispatcher.Invoke(() => result = func(), DispatcherPriority.Background);
                return result;
            }
        }

        private (int left, int top) GetCursorPosition()
        {
            int caretIndex = textBox.CaretIndex;
            string text = textBox.Text;
            
            int left = 0;
            int top = 0;
            
            for (int i = 0; i < caretIndex && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    top++;
                    left = 0;
                }
                else
                {
                    left++;
                }
            }
            
            return (left, top);
        }

        private Color ConvertConsoleColorToColor(ConsoleColor consoleColor)
        {
            return consoleColor switch
            {
                ConsoleColor.Black => Colors.Black,
                ConsoleColor.DarkBlue => Colors.DarkBlue,
                ConsoleColor.DarkGreen => Colors.DarkGreen,
                ConsoleColor.DarkCyan => Colors.DarkCyan,
                ConsoleColor.DarkRed => Colors.DarkRed,
                ConsoleColor.DarkMagenta => Colors.DarkMagenta,
                ConsoleColor.DarkYellow => Colors.Orange,
                ConsoleColor.Gray => Colors.Gray,
                ConsoleColor.DarkGray => Colors.DarkGray,
                ConsoleColor.Blue => Colors.Blue,
                ConsoleColor.Green => Colors.Green,
                ConsoleColor.Cyan => Colors.Cyan,
                ConsoleColor.Red => Colors.Red,
                ConsoleColor.Magenta => Colors.Magenta,
                ConsoleColor.Yellow => Colors.Yellow,
                ConsoleColor.White => Colors.White,
                _ => Colors.Gray
            };
        }

        private ConsoleColor ConvertColorToConsoleColor(Color color)
        {
            // Карта соответствий ConsoleColor -> Color
            var consoleColorMap = new Dictionary<ConsoleColor, Color>
            {
                { ConsoleColor.Black, Colors.Black },
                { ConsoleColor.DarkBlue, Colors.DarkBlue },
                { ConsoleColor.DarkGreen, Colors.DarkGreen },
                { ConsoleColor.DarkCyan, Colors.DarkCyan },
                { ConsoleColor.DarkRed, Colors.DarkRed },
                { ConsoleColor.DarkMagenta, Colors.DarkMagenta },
                { ConsoleColor.DarkYellow, Colors.Orange },
                { ConsoleColor.Gray, Colors.Gray },
                { ConsoleColor.DarkGray, Colors.DarkGray },
                { ConsoleColor.Blue, Colors.Blue },
                { ConsoleColor.Green, Colors.Green },
                { ConsoleColor.Cyan, Colors.Cyan },
                { ConsoleColor.Red, Colors.Red },
                { ConsoleColor.Magenta, Colors.Magenta },
                { ConsoleColor.Yellow, Colors.Yellow },
                { ConsoleColor.White, Colors.White }
            };

            ConsoleColor closestColor = ConsoleColor.Gray;
            double minDistance = double.MaxValue;

            // Вычисляем расстояние до каждого ConsoleColor
            foreach (var kvp in consoleColorMap)
            {
                double distance = CalculateColorDistance(color, kvp.Value);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestColor = kvp.Key;
                }
            }

            return closestColor;
        }

        /// <summary>
        /// Вычисляет евклидово расстояние между двумя цветами в RGB пространстве
        /// </summary>
        private double CalculateColorDistance(Color color1, Color color2)
        {
            double deltaR = color1.R - color2.R;
            double deltaG = color1.G - color2.G;
            double deltaB = color1.B - color2.B;
            
            // Используем квадрат расстояния (без корня) для оптимизации
            // Корень не нужен, так как мы только сравниваем значения
            return deltaR * deltaR + deltaG * deltaG + deltaB * deltaB;
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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Можно добавить дополнительную логику при изменении текста
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


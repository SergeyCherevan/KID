using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KID.Services.Interfaces;

namespace KID.Services
{
    /// <summary>
    /// Реализация IConsole для WPF приложения
    /// </summary>
    public class WpfConsole : IConsole, IDisposable
    {
        private readonly System.Windows.Controls.TextBox textBox;
        private readonly Dispatcher dispatcher;
        
        // Для синхронизации ввода
        private readonly ManualResetEventSlim inputWaitHandle = new ManualResetEventSlim(false);
        private string pendingInput = string.Empty;
        private ConsoleKeyInfo? pendingKey;
        private bool isWaitingForLine = false;
        private bool isWaitingForKey = false;
        private int inputStartPosition = 0;
        
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

        public WpfConsole(System.Windows.Controls.TextBox textBox)
        {
            this.textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            this.dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            
            // Подписываемся на события TextBox для обработки ввода
            textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            textBox.TextChanged += TextBox_TextChanged;
            
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

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (isWaitingForLine)
            {
                // Режим ReadLine - ждем Enter
                if (e.Key == Key.Enter)
                {
                    // Читаем ввод от позиции inputStartPosition до конца
                    var currentText = textBox.Text;
                    if (currentText.Length >= inputStartPosition)
                    {
                        pendingInput = currentText.Substring(inputStartPosition);
                        inputWaitHandle.Set();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    pendingInput = string.Empty;
                    inputWaitHandle.Set();
                    e.Handled = true;
                }
            }
            else if (isWaitingForKey)
            {
                // Режим ReadKey - сохраняем информацию о клавише
                var key = ConvertWpfKeyToConsoleKey(e.Key);
                var keyChar = GetKeyChar(e.Key, e.KeyboardDevice.Modifiers);
                pendingKey = new ConsoleKeyInfo(keyChar, key, 
                    (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) != 0,
                    (e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0,
                    (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0);
                inputWaitHandle.Set();
                e.Handled = true;
            }
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Обновляем позицию курсора для отслеживания ввода
            // Можно добавить дополнительную логику если нужно
        }

        private ConsoleKey ConvertWpfKeyToConsoleKey(Key wpfKey)
        {
            // Простое преобразование основных клавиш
            if (wpfKey >= Key.A && wpfKey <= Key.Z)
                return (ConsoleKey)((int)ConsoleKey.A + (wpfKey - Key.A));
            if (wpfKey >= Key.D0 && wpfKey <= Key.D9)
                return (ConsoleKey)((int)ConsoleKey.D0 + (wpfKey - Key.D0));
            
            // Специальные клавиши
            return wpfKey switch
            {
                Key.Enter => ConsoleKey.Enter,
                Key.Escape => ConsoleKey.Escape,
                Key.Space => ConsoleKey.Spacebar,
                Key.Back => ConsoleKey.Backspace,
                Key.Delete => ConsoleKey.Delete,
                Key.Left => ConsoleKey.LeftArrow,
                Key.Right => ConsoleKey.RightArrow,
                Key.Up => ConsoleKey.UpArrow,
                Key.Down => ConsoleKey.DownArrow,
                _ => ConsoleKey.NoName
            };
        }

        private char GetKeyChar(Key key, ModifierKeys modifiers)
        {
            // Упрощенное преобразование клавиши в символ
            if (key >= Key.A && key <= Key.Z)
            {
                var isShift = (modifiers & ModifierKeys.Shift) != 0;
                var baseChar = (char)('a' + (key - Key.A));
                return isShift ? char.ToUpper(baseChar) : baseChar;
            }
            if (key >= Key.D0 && key <= Key.D9)
            {
                return (char)('0' + (key - Key.D0));
            }
            return key switch
            {
                Key.Space => ' ',
                Key.Enter => '\r',
                Key.Tab => '\t',
                _ => '\0'
            };
        }

        // === Вывод ===
        public void Write(string value)
        {
            if (dispatcher.CheckAccess())
            {
                textBox.AppendText(value);
                textBox.ScrollToEnd();
                OutputReceived?.Invoke(this, value);
            }
            else
            {
                dispatcher.BeginInvoke(() => 
                {
                    textBox.AppendText(value);
                    textBox.ScrollToEnd();
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
            var line = ReadLine();
            return line.Length > 0 ? line[0] : -1;
        }

        public string ReadLine()
        {
            isWaitingForLine = true;
            inputStartPosition = textBox.Text.Length;
            inputWaitHandle.Reset();
            pendingInput = string.Empty;
            
            // Делаем TextBox доступным для редактирования
            if (dispatcher.CheckAccess())
            {
                textBox.IsReadOnly = false;
                textBox.Focus();
            }
            else
            {
                dispatcher.BeginInvoke(() =>
                {
                    textBox.IsReadOnly = false;
                    textBox.Focus();
                }, DispatcherPriority.Normal);
            }
            
            // Ждем ввода (блокирующий вызов)
            inputWaitHandle.Wait();
            
            isWaitingForLine = false;
            var result = pendingInput;
            pendingInput = string.Empty;
            
            // Возвращаем TextBox в режим только для чтения
            if (dispatcher.CheckAccess())
            {
                textBox.IsReadOnly = true;
            }
            else
            {
                dispatcher.BeginInvoke(() => textBox.IsReadOnly = true, DispatcherPriority.Normal);
            }
            
            return result;
        }

        public ConsoleKeyInfo ReadKey()
        {
            return ReadKey(false);
        }

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            isWaitingForKey = true;
            inputWaitHandle.Reset();
            pendingKey = null;
            
            // Делаем TextBox доступным для редактирования
            if (dispatcher.CheckAccess())
            {
                textBox.IsReadOnly = false;
                textBox.Focus();
            }
            else
            {
                dispatcher.BeginInvoke(() =>
                {
                    textBox.IsReadOnly = false;
                    textBox.Focus();
                }, DispatcherPriority.Normal);
            }
            
            // Ждем нажатия клавиши (блокирующий вызов)
            inputWaitHandle.Wait();
            
            isWaitingForKey = false;
            var result = pendingKey ?? new ConsoleKeyInfo('\0', ConsoleKey.NoName, false, false, false);
            pendingKey = null;
            
            // Возвращаем TextBox в режим только для чтения
            if (dispatcher.CheckAccess())
            {
                textBox.IsReadOnly = true;
            }
            else
            {
                dispatcher.BeginInvoke(() => textBox.IsReadOnly = true, DispatcherPriority.Normal);
            }
            
            return result;
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
            if (dispatcher.CheckAccess())
            {
                textBox.Clear();
            }
            else
            {
                dispatcher.BeginInvoke(() => textBox.Clear(), DispatcherPriority.Background);
            }
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
            // Отписываемся от событий
            textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
            textBox.TextChanged -= TextBox_TextChanged;
            
            // Восстанавливаем оригинальные потоки
            System.Console.SetOut(originalOut);
            System.Console.SetIn(originalIn);
            System.Console.SetError(originalError);
            
            // Освобождаем ресурсы
            inputWaitHandle?.Dispose();
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


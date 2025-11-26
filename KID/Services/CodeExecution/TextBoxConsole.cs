using System;
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
        private TextWriter textWriter;
        private TextReader textReader;
        
        // === Поля для ввода ===
        private volatile bool isReading = false;
        private readonly AutoResetEvent keyDownReadEvent = new AutoResetEvent(false);
        private volatile int lastReadChar = -1;
        private readonly object readLock = new object();

        public TextBoxConsole(TextBox textBox)
        {
            this.textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            dispatcher = Application.Current.Dispatcher;
            
            // Инициализация потоков
            textWriter = new TextBoxTextWriter(this);
            textReader = new TextBoxTextReader(this);
            
            // Настройка TextBox для ввода
            textBox.KeyDown += TextBox_KeyDown;
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

        // === Вывод ===
        public void Write(char symbol)
        {
            InvokeOnUIThread(() =>
            {
                textBox.AppendText(symbol.ToString());
                textBox.ScrollToEnd();
                OutputReceived?.Invoke(this, symbol.ToString());
            });
        }

        // === Ввод ===
        public int Read()
        {
            lock (readLock)
            {
                isReading = true;
                
                // Ждем события (блокируем поток)
                keyDownReadEvent.WaitOne();
                
                isReading = false;
                
                int result = lastReadChar;
                lastReadChar = -1; // Очищаем
                
                return result;
            }
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

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!isReading) return; // Игнорируем, если не ждем ввода
            
            // Преобразуем клавишу в символ
            char? symbol = ConvertKeyToChar(e.Key, e.KeyboardDevice.Modifiers);
            
            if (symbol.HasValue)
            {
                // Показываем символ в TextBox (уже в UI потоке)
                textBox.AppendText(symbol.Value.ToString());
                textBox.ScrollToEnd();
                
                // Сохраняем символ и сигналим
                lastReadChar = symbol.Value;
                keyDownReadEvent.Set(); // "Будим" Read()
                
                e.Handled = true; // Предотвращаем стандартную обработку
            }
        }
        
        /// <summary>
        /// Преобразует клавишу в символ
        /// </summary>
        private char? ConvertKeyToChar(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            // Обработка Enter
            if (key == System.Windows.Input.Key.Enter)
            {
                return '\n';
            }
            
            // Обработка Backspace (опционально, можно вернуть '\b')
            if (key == System.Windows.Input.Key.Back)
            {
                return '\b';
            }
            
            // Пропускаем служебные клавиши
            if (key >= System.Windows.Input.Key.LeftShift && key <= System.Windows.Input.Key.RightAlt)
                return null;
            
            if (key >= System.Windows.Input.Key.LeftCtrl && key <= System.Windows.Input.Key.RightCtrl)
                return null;
            
            // Преобразуем клавишу в символ
            bool isShift = modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift);
            bool isCapsLock = System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.CapsLock);
            
            // Базовые символы
            if (key >= System.Windows.Input.Key.A && key <= System.Windows.Input.Key.Z)
            {
                bool upper = (isShift && !isCapsLock) || (!isShift && isCapsLock);
                return (char)(upper ? (int)key - (int)System.Windows.Input.Key.A + 'A' 
                                    : (int)key - (int)System.Windows.Input.Key.A + 'a');
            }
            
            // Цифры
            if (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
            {
                char digit = (char)((int)key - (int)System.Windows.Input.Key.D0 + '0');
                if (isShift)
                {
                    // Символы с Shift для цифр
                    return digit switch
                    {
                        '0' => ')',
                        '1' => '!',
                        '2' => '@',
                        '3' => '#',
                        '4' => '$',
                        '5' => '%',
                        '6' => '^',
                        '7' => '&',
                        '8' => '*',
                        '9' => '(',
                        _ => digit
                    };
                }
                return digit;
            }
            
            // Пробел
            if (key == System.Windows.Input.Key.Space)
            {
                return ' ';
            }
            
            // Специальные символы (упрощенная версия)
            return key switch
            {
                System.Windows.Input.Key.OemMinus => isShift ? '_' : '-',
                System.Windows.Input.Key.OemPlus => isShift ? '+' : '=',
                System.Windows.Input.Key.OemOpenBrackets => isShift ? '{' : '[',
                System.Windows.Input.Key.OemCloseBrackets => isShift ? '}' : ']',
                System.Windows.Input.Key.OemPipe => isShift ? '|' : '\\',
                System.Windows.Input.Key.OemSemicolon => isShift ? ':' : ';',
                System.Windows.Input.Key.OemQuotes => isShift ? '"' : '\'',
                System.Windows.Input.Key.OemComma => isShift ? '<' : ',',
                System.Windows.Input.Key.OemPeriod => isShift ? '>' : '.',
                System.Windows.Input.Key.OemQuestion => isShift ? '?' : '/',
                System.Windows.Input.Key.Tab => '\t',
                _ => null
            };
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

            public override void Write(char symbol)
            {
                console.Write(symbol);
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
        }
    }
}


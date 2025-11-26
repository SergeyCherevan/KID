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
            textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            textBox.PreviewTextInput += TextBox_PreviewTextInput;
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
        public void Write(char value)
        {
            InvokeOnUIThread(() =>
            {
                textBox.AppendText(value.ToString());
                textBox.ScrollToEnd();
                OutputReceived?.Invoke(this, value.ToString());
            });
        }
        public void Write(string? value)
        {
            if (value == null) return;
            
            InvokeOnUIThread(() =>
            {
                textBox.AppendText(value);
                textBox.ScrollToEnd();
                OutputReceived?.Invoke(this, value);
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
                
                char result = (char)lastReadChar;

                InvokeOnUIThread(() =>
                {
                    textBox.AppendText(result.ToString());
                    textBox.ScrollToEnd();
                });

                lastReadChar = -1; // Очищаем
                
                return (int)result;
            }
        }

        public string ReadLine()
        {
            lock (readLock)
            {
                StringBuilder result = new StringBuilder();
                isReading = true;
                
                char symbol;
                do
                {
                    keyDownReadEvent.WaitOne();

                    CancellationManager.CheckCancellation();
                    
                    // Ждем события (блокируем поток)
                    symbol = (char)lastReadChar;
                    lastReadChar = -1; // Очищаем сразу после чтения

                    if (symbol == '\b')
                    {
                        // Обработка Backspace
                        if (result.Length > 0)
                        {
                            InvokeOnUIThread(() =>
                            {
                                if (textBox.Text.Length > 0)
                                {
                                    textBox.Text = textBox.Text.Substring(0, textBox.Text.Length - 1);
                                    textBox.CaretIndex = textBox.Text.Length;
                                    textBox.ScrollToEnd();
                                }
                            });
                            result.Remove(result.Length - 1, 1);
                        }
                        // Если result.Length == 0, просто игнорируем Backspace
                    }
                    else
                    {
                        // Обычный символ (не Enter и не Backspace)
                        InvokeOnUIThread(() =>
                        {
                            textBox.AppendText(symbol.ToString());
                            textBox.ScrollToEnd();
                        });

                        if (symbol != '\n')
                        {
                            result.Append(symbol);
                        }
                    }
                }
                while (symbol != '\n');

                isReading = false;

                return result.ToString();
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

        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!isReading) return; // Игнорируем, если не ждем ввода
            
            // Обрабатываем только специальные клавиши (Enter, Backspace)
            // Текстовые символы обрабатываются через TextInput
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                lastReadChar = '\n';
                keyDownReadEvent.Set();
                e.Handled = true;
            }
            if (e.Key == System.Windows.Input.Key.Space)
            {
                lastReadChar = ' ';
                keyDownReadEvent.Set();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Back)
            {
                lastReadChar = '\b';
                keyDownReadEvent.Set();
                e.Handled = true;
            }
        }
        
        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!isReading) return; // Игнорируем, если не ждем ввода
            
            // Получаем текстовый символ (работает с кириллицей и любыми Unicode символами)
            if (e.Text.Length > 0)
            {
                char symbol = e.Text[0];
                lastReadChar = symbol;
                keyDownReadEvent.Set(); // "Будим" Read()
                e.Handled = true; // Предотвращаем стандартную обработку
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

            public override void Write(char symbol)
            {
                console.Write(symbol);
            }

            public override void Write(string? value)
            {
                console.Write(value);
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


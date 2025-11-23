using System;
using System.IO;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;

namespace KID.Services.CodeExecution
{
    public class ConsoleRedirector : TextWriter
    {
        private readonly TextBox textBox;
        private readonly Dispatcher dispatcher;

        public ConsoleRedirector(TextBox textBox)
        {
            this.textBox = textBox;
            dispatcher = Application.Current.Dispatcher;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string value)
        {
            if (dispatcher.CheckAccess())
            {
                textBox.AppendText(value);
                textBox.ScrollToEnd();
            }
            else
            {
                dispatcher.BeginInvoke(() =>
                {
                    textBox.AppendText(value);
                    textBox.ScrollToEnd();
                }, DispatcherPriority.Background);
            }
        }

        public override void Write(char value)
        {
            Write(value.ToString());
        }

        public override void WriteLine(string value)
        {
            Write(value + Environment.NewLine);
        }

        public override void WriteLine(char value)
        {
            WriteLine(value.ToString());
        }
    }
}
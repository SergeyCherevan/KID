using System;
using System.IO;
using System.Text;
using System.Windows.Threading;
using System.Windows;

namespace KID.Services
{
    public class ConsoleRedirector : TextWriter
    {
        private readonly Action<string> output;
        private readonly Dispatcher dispatcher;

        public ConsoleRedirector(Action<string> output)
        {
            this.output = output;
            this.dispatcher = Application.Current.Dispatcher;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string value)
        {
            if (dispatcher.CheckAccess())
            {
                output(value);
            }
            else
            {
                dispatcher.BeginInvoke(() => output(value), DispatcherPriority.Background);
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
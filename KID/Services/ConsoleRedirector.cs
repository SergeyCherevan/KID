using System;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace KID.Services
{
    public class ConsoleRedirector : TextWriter
    {
        private readonly Action<string> output;
        private readonly Dispatcher dispatcher;

        public ConsoleRedirector(Action<string> output)
        {
            this.output = output;
            this.dispatcher = Dispatcher.CurrentDispatcher;
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
                dispatcher.Invoke(() => output(value));
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
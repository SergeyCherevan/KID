using System;
using System.IO;
using System.Text;

namespace KID.Services
{
    public class ConsoleRedirector : TextWriter
    {
        private readonly Action<string> output;

        public ConsoleRedirector(Action<string> output)
        {
            this.output = output;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value)
        {
            output(value);
        }

        public override void Write(char value)
        {
            output(value.ToString());
        }
    }
}

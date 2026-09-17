using System.IO;
using System.Text;

namespace KID;

public static partial class TextBoxConsole
{
    private sealed class TextBoxTextWriter(ConsoleExecutionScope scope) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) => TextBoxConsole.Write(scope, value.ToString());
        public override void Write(string? value) => TextBoxConsole.Write(scope, value);
    }

    private sealed class TextBoxTextReader(ConsoleExecutionScope scope) : TextReader
    {
        public override int Read() => TextBoxConsole.Read(scope);
        public override string ReadLine() => TextBoxConsole.ReadLine(scope);
    }
}

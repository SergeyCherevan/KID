using System.IO;
using System.Text;

namespace KID;

public static partial class KIDConsole
{
    /// <summary>
    /// Поток вывода одного запуска. Захваченный scope передаётся каждому Write напрямую:
    /// adapter никогда не перенаправляет старый вызов в текущую console-сессию.
    /// </summary>
    private sealed class TextBoxTextWriter(ConsoleExecutionScope scope) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) => KIDConsole.Write(scope, value.ToString());
        public override void Write(string? value) => KIDConsole.Write(scope, value);
    }

    /// <summary>
    /// Поток ввода одного запуска. После release захваченного scope чтение завершается
    /// <see cref="ObjectDisposedException"/> и не может получить input следующего запуска.
    /// </summary>
    private sealed class TextBoxTextReader(ConsoleExecutionScope scope) : TextReader
    {
        public override int Read() => KIDConsole.Read(scope);
        public override string ReadLine() => KIDConsole.ReadLine(scope);
    }
}

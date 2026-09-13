using System.IO;
using System.Text;

namespace KID.Services.CodeExecution.Console;

public sealed partial class TextBoxConsole
{
    /// <summary>
    /// Адаптирует потоковые операции <see cref="TextWriter"/> к методам владельца
    /// <see cref="TextBoxConsole"/>.
    /// </summary>
    /// <remarks>
    /// Адаптер не владеет отдельной очередью или lifecycle: сохранённая ссылка всегда направляет
    /// Write обратно в консоль, где выполняются проверки Dispose и execution ownership.
    /// </remarks>
    private sealed class TextBoxTextWriter(TextBoxConsole console) : TextWriter
    {
        /// <summary>
        /// Возвращает обязательное для <see cref="TextWriter"/> описание кодировки потока.
        /// </summary>
        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>Передаёт один UTF-16 символ владельцу консоли.</summary>
        /// <param name="value">Символ для вывода.</param>
        public override void Write(char value) => console.Write(value);

        /// <summary>Передаёт строковый фрагмент владельцу консоли.</summary>
        /// <param name="value">Строка для вывода; может быть <see langword="null"/>.</param>
        public override void Write(string? value) => console.Write(value);
    }

    /// <summary>
    /// Адаптирует синхронные операции <see cref="TextReader"/> к управляемому WPF-вводу
    /// владельца <see cref="TextBoxConsole"/>.
    /// </summary>
    private sealed class TextBoxTextReader(TextBoxConsole console) : TextReader
    {
        /// <summary>Ожидает один UTF-16 code unit через владельца консоли.</summary>
        /// <returns>Числовое значение следующего символа.</returns>
        public override int Read() => console.Read();

        /// <summary>Ожидает строку до Enter через владельца консоли.</summary>
        /// <returns>Введённая строка без завершающего Enter.</returns>
        public override string ReadLine() => console.ReadLine();
    }
}

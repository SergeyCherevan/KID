using System.Windows.Controls;

namespace KID;

/// <summary>
/// Связывает TextBoxConsole runtime одного запуска с его environment, WPF TextBox
/// и последовательным worker пользовательских событий.
/// </summary>
internal sealed class ConsoleExecutionScope
{
    internal ConsoleExecutionScope(ExecutionEnvironment environment, TextBox textBox)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        TextBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
        EventWorker = new ExecutionEventWorker(environment, "Console");
    }

    internal ExecutionEnvironment Environment { get; }
    internal TextBox TextBox { get; }
    internal ExecutionEventWorker EventWorker { get; }
}

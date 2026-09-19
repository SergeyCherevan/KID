using System.Windows;

namespace KID;

/// <summary>Связывает Keyboard runtime одного запуска с его environment и WPF Window.</summary>
internal sealed class KeyboardExecutionScope
{
    internal KeyboardExecutionScope(ExecutionEnvironment environment, Window window)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Window = window ?? throw new ArgumentNullException(nameof(window));
        EventWorker = new ExecutionEventWorker(environment, "Keyboard");
    }

    internal ExecutionEnvironment Environment { get; }
    internal Window Window { get; }
    internal ExecutionEventWorker EventWorker { get; }
}

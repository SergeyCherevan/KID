using System.Windows.Controls;

namespace KID;

/// <summary>Связывает Mouse runtime одного запуска с его environment и WPF Canvas.</summary>
internal sealed class MouseExecutionScope
{
    internal MouseExecutionScope(ExecutionEnvironment environment, Canvas canvas)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        EventWorker = new ExecutionEventWorker(environment);
    }

    internal ExecutionEnvironment Environment { get; }
    internal Canvas Canvas { get; }
    internal ExecutionEventWorker EventWorker { get; }
}

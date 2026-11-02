using System.Windows;
using System.Windows.Controls;

namespace KID;

/// <summary>Explicit, optional resources supplied by a standalone WPF host.</summary>
public sealed class KIDRuntimeOptions
{
    public Canvas? Canvas { get; init; }
    public Canvas? MouseCanvas { get; init; }
    public Window? KeyboardWindow { get; init; }
    public bool EnableMusic { get; init; }
    public TextBox? ConsoleTextBox { get; init; }
    public bool RedirectSystemConsole { get; init; }

    /// <summary>May run on any thread. Exceptions from this callback are traced.</summary>
    public Action<KIDRuntimeDiagnostic>? DiagnosticSink { get; init; }
}

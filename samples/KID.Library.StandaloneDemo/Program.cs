using System.Windows;

namespace KID.StandaloneDemo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(arg => arg is not ("--with-console" or "--verify")))
        {
            Console.Error.WriteLine("Usage: KID.Standalone.Demo.D005 [--with-console] [--verify]");
            return 2;
        }

        var app = new Application();
        var window = new MainWindow(args.Contains("--with-console"), args.Contains("--verify"));
        app.Run(window);
        return window.ExitCode;
    }
}

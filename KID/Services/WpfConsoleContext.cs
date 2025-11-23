using System;
using System.Windows.Controls;
using KID.Services.Interfaces;

namespace KID.Services
{
    /// <summary>
    /// Реализация консольного контекста для WPF TextBox
    /// </summary>
    public class WpfConsoleContext : IConsoleContext
    {
        public IConsole CreateConsole(object consoleTarget)
        {
            if (consoleTarget is TextBox textBox)
            {
                return new WpfConsole(textBox);
            }
            else
            {
                throw new ArgumentException(
                    $"Ожидался TextBox, получен {consoleTarget?.GetType().Name ?? "null"}", 
                    nameof(consoleTarget));
            }
        }
    }
}


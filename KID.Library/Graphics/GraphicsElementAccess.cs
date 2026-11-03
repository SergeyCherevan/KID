using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KID;

internal static class GraphicsElementAccess
{
    internal static void ValidateDispatcher(UIElement element, DispatcherScope scope)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!ReferenceEquals(element.Dispatcher, scope.Dispatcher))
            throw new ArgumentException("The element must use the session Dispatcher.", nameof(element));
    }

    // Call on the owning UI thread. A logical parent also counts before a template is applied.
    internal static DependencyObject? GetParent(UIElement element) =>
        VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);

    internal static Canvas? GetParentCanvas(UIElement element) => GetParent(element) switch
    {
        null => null,
        Canvas canvas => canvas,
        _ => throw new InvalidOperationException("The element must be a direct child of a Canvas or detached.")
    };
}

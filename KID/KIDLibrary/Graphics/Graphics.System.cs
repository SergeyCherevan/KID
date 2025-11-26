using System.Windows.Media;

namespace KID
{
    public static partial class Graphics
    {
        public static void Clear()
        {
            InvokeOnUI(() =>
            {
                Canvas.Children.Clear();
            });
        }
    }
}

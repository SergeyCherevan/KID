using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KID.Converters
{
    /// <summary>
    /// Конвертер WindowState -> Thickness.
    /// При максимизации возвращает отступ, чтобы рамка окна оставалась видимой
    /// (окно при максимизации расширяется за пределы экрана, и рамка уходит за край).
    /// </summary>
    public class WindowStateToMarginConverter : IValueConverter
    {
        /// <summary>
        /// Отступ в пикселях при максимизации (рамка остаётся видимой).
        /// </summary>
        private const int MaximizedMargin = 5;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowState state && state == WindowState.Maximized)
                return new Thickness(MaximizedMargin, 0, MaximizedMargin, MaximizedMargin);

            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

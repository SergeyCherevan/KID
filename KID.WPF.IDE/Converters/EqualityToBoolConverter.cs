using System;
using System.Globalization;
using System.Windows.Data;

namespace KID.Converters
{
    /// <summary>
    /// Конвертер для сравнения двух значений в MultiBinding.
    /// Возвращает true, если значения равны; иначе false.
    /// Используется для отображения галочки у выбранного пункта меню.
    /// </summary>
    public class EqualityToBoolConverter : IMultiValueConverter
    {
        private const double DoubleTolerance = 0.001;

        /// <summary>
        /// Сравнивает values[0] (текущее значение из ViewModel) с values[1] (значение пункта меню).
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var current = values[0];
            var item = values[1];

            if (current == null && item == null)
                return true;
            if (current == null || item == null)
                return false;

            if (current is double d1 && item is double d2)
                return Math.Abs(d1 - d2) < DoubleTolerance;

            return Equals(current, item);
        }

        /// <summary>
        /// Обратное преобразование не поддерживается (только чтение).
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

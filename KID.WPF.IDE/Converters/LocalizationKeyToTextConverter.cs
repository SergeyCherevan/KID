using System;
using System.Globalization;
using System.Windows.Data;
using KID.Services.Localization.Interfaces;
using KID.Services.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KID.Converters
{
    /// <summary>
    /// Конвертирует строковый ключ локализации в локализованное значение.
    /// Второй аргумент в MultiBinding используется как триггер переоценки при смене языка.
    /// </summary>
    public class LocalizationKeyToTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] is not string key || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            try
            {
                var service = App.ServiceProvider?.GetRequiredService<ILocalizationService>();
                return service?.GetString(key) ?? $"[{key}]";
            }
            catch (Exception exception)
            {
                App.ServiceProvider?.GetService<ILogger<LocalizationKeyToTextConverter>>()?.LogWarning(
                    DiagnosticEventIds.LocalizationFallback,
                    exception,
                    "Localization multivalue converter failed. Key={Key}",
                    key);
                return $"[{key}]";
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

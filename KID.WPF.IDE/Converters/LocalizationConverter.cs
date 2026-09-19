using System;
using System.Globalization;
using System.Windows.Data;
using KID.Services.Localization.Interfaces;
using KID.Services.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KID.Converters
{
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string key && !string.IsNullOrEmpty(key))
            {
                try
                {
                    var service = App.ServiceProvider?.GetRequiredService<ILocalizationService>();
                    return service?.GetString(key) ?? $"[{key}]";
                }
                catch (Exception exception)
                {
                    App.ServiceProvider?.GetService<ILogger<LocalizationConverter>>()?.LogWarning(
                        DiagnosticEventIds.LocalizationFallback,
                        exception,
                        "Localization converter failed. Key={Key}",
                        key);
                    return $"[{key}]";
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


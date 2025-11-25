using System;
using System.Globalization;
using System.Windows.Data;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.DependencyInjection;

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
                catch
                {
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


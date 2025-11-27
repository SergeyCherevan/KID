using System;
using System.Windows.Data;
using System.Windows.Markup;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KID.Services.Localization
{
    public class LocalizationExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public LocalizationExtension() { }
        
        public LocalizationExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            try
            {
                var service = App.ServiceProvider?.GetRequiredService<ILocalizationService>();
                if (service == null)
                    return $"[{Key}]";

                // Создаем Binding, которое будет обновляться при смене культуры
                var binding = new Binding(nameof(ILocalizationService.CurrentCulture))
                {
                    Source = service,
                    Converter = new LocalizationValueConverter(service, Key),
                    Mode = BindingMode.OneWay
                };

                return binding.ProvideValue(serviceProvider);
            }
            catch
            {
                return $"[{Key}]";
            }
        }

        private class LocalizationValueConverter : IValueConverter
        {
            private readonly ILocalizationService _service;
            private readonly string _key;

            public LocalizationValueConverter(ILocalizationService service, string key)
            {
                _service = service ?? throw new ArgumentNullException(nameof(service));
                _key = key ?? throw new ArgumentNullException(nameof(key));
            }

            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (_service == null || string.IsNullOrEmpty(_key))
                    return $"[{_key}]";
                
                return _service.GetString(_key);
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}


using System;
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
                return service?.GetString(Key) ?? $"[{Key}]";
            }
            catch
            {
                return $"[{Key}]";
            }
        }
    }
}


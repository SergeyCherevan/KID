using System;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;

namespace KID.Services.DI
{
    public class ServiceProviderExtension : MarkupExtension
    {
        /// <summary>
        /// Тип сервиса, который нужно получить из контейнера приложения для использования в XAML.
        /// WPF присваивает значение из разметки после создания расширения, поэтому до вызова
        /// <see cref="ProvideValue(IServiceProvider)"/> свойство может оставаться <see langword="null"/>.
        /// </summary>
        public Type? ServiceType { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (ServiceType == null)
                throw new InvalidOperationException("ServiceType must be specified");

            return App.ServiceProvider.GetRequiredService(ServiceType);
        }
    }
}

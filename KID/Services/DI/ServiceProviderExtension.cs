using System;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;

namespace KID.Services.DI
{
    public class ServiceProviderExtension : MarkupExtension
    {
        public Type ServiceType { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (ServiceType == null)
                throw new InvalidOperationException("ServiceType must be specified");

            return App.ServiceProvider.GetRequiredService(ServiceType);
        }
    }
}

using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ark.AspNetCore.MessagePackFormatter
{
    public static class Ex
    {
        public static void AddMessagePackFormatter(this IServiceCollection services, IFormatterResolver resolver = null)
        {
            if (resolver != null)
                services.TryAddSingleton<IFormatterResolver>(resolver);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, MessagePackFormatterSetup>());
        }
    }
}

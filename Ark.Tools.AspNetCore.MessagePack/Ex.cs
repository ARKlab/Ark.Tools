// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using MessagePack;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.MessagePackFormatter
{
    public static class Ex
    {
        public static void AddMessagePackFormatter(this IServiceCollection services, IFormatterResolver? resolver = null)
        {
            if (resolver != null)
                services.TryAddSingleton<IFormatterResolver>(resolver);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, MessagePackFormatterSetup>());
        }
    }
}

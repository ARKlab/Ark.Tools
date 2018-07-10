// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.XlsxOutputFormatter.Serialisation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter
{
    public static class XlsxOutputFormatterServicesExtensions
    {
        public static void AddXlsxOutputFormatter(this IServiceCollection services, Action<XlsxOutputFormatterOptions> setupAction)
        {
            services.Configure(setupAction);
            services.AddSingleton<IColumnResolver, DefaultColumnResolver>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IXlsxSerialiser, ExpandoSerialiser>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IXlsxSerialiser, SimpleTypeXlsxSerialiser>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IXlsxSerialiser, DefaultXlsxSerialiser>());
            services.AddTransient<Func<IEnumerable<IXlsxSerialiser>>>(provider => () => provider.GetRequiredService<IEnumerable<IXlsxSerialiser>>());
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, XlsxOutputFormatterSetup>());
        }
    }
}

// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public static partial class NodaTimeSqlServerExtensions
    {
        public static IServiceCollection SetNodaTimeSqlServerMappingSource(this IServiceCollection serviceCollection)
        {
            var descriptor2 = serviceCollection.FirstOrDefault(d => d.ServiceType == typeof(IRelationalTypeMappingSource));
            serviceCollection.Remove(descriptor2);
            serviceCollection.AddSingleton<IRelationalTypeMappingSource, NodaTimeSqlServerTypeMappingSource>();
            return serviceCollection;
        }

        public static DbContextOptionsBuilder AddNodaTimeSqlServer(this DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ReplaceService<IRelationalTypeMappingSource, NodaTimeSqlServerTypeMappingSource>();
            optionsBuilder.ReplaceService<IMemberTranslator, NodaTimeSqlServerCompositeMemberTranslator>();
            optionsBuilder.ReplaceService<ICompositeMethodCallTranslator, NodaTimeSqlServerCompositeMethodCallTranslator>();
            return optionsBuilder;
        }
    }

}

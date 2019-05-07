// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime
{
    public class NodaTimeSqlServerTypeMappingSource : SqlServerTypeMappingSource
    {
        public NodaTimeSqlServerTypeMappingSource(TypeMappingSourceDependencies dependencies, RelationalTypeMappingSourceDependencies relationalDependencies)
            : base(dependencies, relationalDependencies)
        {
        }

        protected override RelationalTypeMapping FindMapping(in RelationalTypeMappingInfo mappingInfo)
            => FindNodaTimeMapping(mappingInfo)?.Clone(mappingInfo)
                ?? base.FindMapping(mappingInfo);

        private static readonly RelationalTypeMapping _localDate = new SqlServerLocalDateRelationalTypeMapping("date");
        private static readonly RelationalTypeMapping _localDateTime = new SqlServerLocalDateTimeRelationalTypeMapping("datetime2");
        private static readonly RelationalTypeMapping _instant = new SqlServerInstantRelationalTypeMapping("datetime2");
        private static readonly RelationalTypeMapping _offsetDateTime = new SqlServerOffsetDateTimeRelationalTypeMapping("datetimeoffset");

        private static readonly Dictionary<Type, RelationalTypeMapping> _clrTypeMappings = new Dictionary<Type, RelationalTypeMapping>
        {
            { typeof(LocalDate), _localDate },
            { typeof(LocalDateTime), _localDateTime},
            { typeof(Instant), _instant },
            { typeof(OffsetDateTime), _offsetDateTime }
        };

        private static readonly Dictionary<string, RelationalTypeMapping[]> _storeTypeMappings
                = new Dictionary<string, RelationalTypeMapping[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "date", new[]{ _localDate } },
                    { "datetime2", new[]{ _instant, _localDateTime } },
                    { "datetimeoffset", new[]{ _offsetDateTime } },
                };

        private RelationalTypeMapping FindNodaTimeMapping(RelationalTypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;
            var storeTypeName = mappingInfo.StoreTypeName;
            var storeTypeNameBase = mappingInfo.StoreTypeNameBase;


            if (storeTypeName != null)
            {
                if (_storeTypeMappings.TryGetValue(storeTypeName, out var mapping)
                    || _storeTypeMappings.TryGetValue(storeTypeNameBase, out mapping))
                {
                    if (clrType == null)
                        return mapping[0];
                    else
                        return mapping.FirstOrDefault(x => x.ClrType == clrType);
                }
            }

            if (clrType != null)
            {
                if (_clrTypeMappings.TryGetValue(clrType, out var mapping))
                {
                    return mapping;
                }
            }

            return null;
        }
    }

}

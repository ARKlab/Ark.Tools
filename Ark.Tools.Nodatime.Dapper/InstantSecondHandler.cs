﻿using Dapper;

using NodaTime;

using System;
using System.Data;

namespace Ark.Tools.Nodatime.Dapper
{
    public sealed class InstantSecondHandler : SqlMapper.TypeHandler<Instant>
    {
        private InstantSecondHandler()
        {
        }

        public static readonly InstantSecondHandler Instance = new InstantSecondHandler();

        public event EventHandler<IDbDataParameter>? OnSetValue;

        public override void SetValue(IDbDataParameter parameter, Instant value)
        {
            parameter.Value = value.ToUnixTimeSeconds();
            //DbType could throw depending on the Driver implementation
            // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
            //parameter.DbType = DbType.DateTime;

            OnSetValue?.Invoke(this, parameter);
        }

        public override Instant Parse(object? value)
        {         
            if (value == null || value is DBNull) return default;

            if (value is long l)
            {
                return Instant.FromUnixTimeSeconds(l);
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant");
        }
    }
}

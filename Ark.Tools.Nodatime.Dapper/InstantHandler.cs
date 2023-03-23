using Dapper;

using NodaTime;
using NodaTime.Text;

using System;
using System.ComponentModel;
using System.Data;

namespace Ark.Tools.Nodatime.Dapper
{
    public sealed class InstantHandler : SqlMapper.TypeHandler<Instant>
    {
        private InstantHandler()
        {
        }

        public static readonly InstantHandler Instance = new InstantHandler();

        public event EventHandler<IDbDataParameter>? OnSetValue;

        public override void SetValue(IDbDataParameter parameter, Instant value)
        {
            parameter.Value = value.ToDateTimeUtc();
            //DbType could throw depending on the Driver implementation
            // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
            //parameter.DbType = DbType.DateTime;

            OnSetValue?.Invoke(this, parameter);
        }

        public override Instant Parse(object? value)
        {
            if (value == null || value is DBNull) return default;

            if (value is DateTime dateTime)
            {
                var dt = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                return Instant.FromDateTimeUtc(dt);
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return Instant.FromDateTimeOffset(dateTimeOffset);
            }

            if (value is string s)
            {
                var conv = TypeDescriptor.GetConverter(typeof(Instant));
                if (conv?.CanConvertFrom(typeof(string)) == true)
                    return (Instant)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant"));
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant");
        }
    }
}

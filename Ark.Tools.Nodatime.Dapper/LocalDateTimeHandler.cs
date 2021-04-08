using Dapper;

using NodaTime;

using System;
using System.ComponentModel;
using System.Data;

namespace Ark.Tools.Nodatime.Dapper
{
    public sealed class LocalDateTimeHandler : SqlMapper.TypeHandler<LocalDateTime>
    {
        private LocalDateTimeHandler()
        {
        }

        public static readonly LocalDateTimeHandler Instance = new LocalDateTimeHandler();

        public event EventHandler<IDbDataParameter> OnSetValue;

        public override void SetValue(IDbDataParameter parameter, LocalDateTime value)
        {
            parameter.Value = value.ToDateTimeUnspecified();
            //DbType could throw depending on the Driver implementation
            // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
            //parameter.DbType = DbType.DateTime;

            OnSetValue?.Invoke(this, parameter);
        }

        public override LocalDateTime Parse(object value)
        {
            if (value == null || value is DBNull) return default;

            if (value is DateTime dateTime)
            {
                return LocalDateTime.FromDateTime(dateTime);
            }

            if (value is string s)
            {
                var conv = TypeDescriptor.GetConverter(typeof(LocalDateTime));
                if (conv?.CanConvertFrom(typeof(string)) == true)
                    return (LocalDateTime)conv.ConvertFromString(s);
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime");
        }
    }
}

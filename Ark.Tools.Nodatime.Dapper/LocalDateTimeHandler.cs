using Dapper;

using NodaTime;

using System;
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
            parameter.DbType = DbType.DateTime2;

            OnSetValue?.Invoke(this, parameter);
        }

        public override LocalDateTime Parse(object value)
        {
            if (value is DateTime dateTime)
            {
                return LocalDateTime.FromDateTime(dateTime);
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime");
        }
    }
}

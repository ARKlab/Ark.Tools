using Dapper;

using NodaTime;

using System;
using System.Data;

namespace Ark.Tools.Nodatime.Dapper
{
    public sealed class LocalDateHandler : SqlMapper.TypeHandler<LocalDate>
    {
        private LocalDateHandler()
        {
        }

        public static readonly LocalDateHandler Instance = new LocalDateHandler();

        public event EventHandler<IDbDataParameter> OnSetValue;

        public override void SetValue(IDbDataParameter parameter, LocalDate value)
        {
            parameter.Value = value.AtMidnight().ToDateTimeUnspecified();
            parameter.DbType = DbType.Date;

            OnSetValue?.Invoke(this, parameter);
        }

        public override LocalDate Parse(object value)
        {
            if (value is DateTime dateTime)
            {
                return LocalDateTime.FromDateTime(dateTime).Date;
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDate");
        }
    }
}

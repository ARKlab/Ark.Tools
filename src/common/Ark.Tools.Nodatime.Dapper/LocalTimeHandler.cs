using Dapper;

using NodaTime;

using System.ComponentModel;
using System.Data;

namespace Ark.Tools.Nodatime.Dapper;


public sealed class LocalTimeHandler : SqlMapper.TypeHandler<LocalTime>
{
    private LocalTimeHandler()
    {
    }

    public static readonly LocalTimeHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
    public event EventHandler<IDbDataParameter>? OnSetValue;

    public override void SetValue(IDbDataParameter parameter, LocalTime value)
    {
        parameter.Value = TimeSpan.FromTicks(value.TickOfDay);
        //DbType could throw depending on the Driver implementation
        // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
        //parameter.DbType = DbType.Time;

        OnSetValue?.Invoke(this, parameter);
    }

    public override LocalTime Parse(object? value)
    {
        if (value == null || value is DBNull) return default;

        if (value is TimeSpan timeSpan)
        {
            return LocalTime.FromTicksSinceMidnight(timeSpan.Ticks);
        }

        if (value is DateTime dateTime)
        {
            return LocalTime.FromTicksSinceMidnight(dateTime.TimeOfDay.Ticks);
        }

        if (value is string s)
        {
            var conv = TypeDescriptor.GetConverter(typeof(LocalTime));
            if (conv?.CanConvertFrom(typeof(string)) == true)
                return (LocalTime)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalTime"));
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalTime");
    }
}
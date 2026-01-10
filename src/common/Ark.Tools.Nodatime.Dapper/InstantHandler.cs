using Dapper;

using NodaTime;

using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Nodatime.Dapper;

public sealed class InstantHandler : SqlMapper.TypeHandler<Instant>
{
    private InstantHandler()
    {
    }

    public static readonly InstantHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
    public event EventHandler<IDbDataParameter>? OnSetValue;

    public override void SetValue(IDbDataParameter parameter, Instant value)
    {
        parameter.Value = value.ToDateTimeUtc();
        //DbType could throw depending on the Driver implementation
        // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
        //parameter.DbType = DbType.DateTime;

        OnSetValue?.Invoke(this, parameter);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "The TypeConverter for NodaTime.Instant is statically registered in Ark.Tools.Nodatime and will not be trimmed. The Instant type is a known NodaTime struct with a well-defined TypeConverter.")]
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
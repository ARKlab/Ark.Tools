using Dapper;

using NodaTime;

using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Tools.Nodatime.Dapper;

public sealed class OffsetDateTimeHandler : SqlMapper.TypeHandler<OffsetDateTime>
{
    private OffsetDateTimeHandler()
    {
    }

    public static readonly OffsetDateTimeHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
    public event EventHandler<IDbDataParameter>? OnSetValue;

    public override void SetValue(IDbDataParameter parameter, OffsetDateTime value)
    {
        parameter.Value = value.ToDateTimeOffset();
        //DbType could throw depending on the Driver implementation
        // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
        //parameter.DbType = DbType.DateTimeOffset;

        OnSetValue?.Invoke(this, parameter);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "The TypeConverter for NodaTime.OffsetDateTime is statically registered in Ark.Tools.Nodatime and will not be trimmed. The OffsetDateTime type is a known NodaTime struct with a well-defined TypeConverter.")]
    public override OffsetDateTime Parse(object? value)
    {
        if (value == null || value is DBNull) return default;

        if (value is DateTimeOffset dateTimeOffset)
        {
            return OffsetDateTime.FromDateTimeOffset(dateTimeOffset);
        }

        if (value is string s)
        {
            var conv = TypeDescriptor.GetConverter(typeof(OffsetDateTime));
            if (conv?.CanConvertFrom(typeof(string)) == true)
                return (OffsetDateTime)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.OffsetDateTime"));
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.OffsetDateTime");
    }
}
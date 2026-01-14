using Dapper;

using NodaTime;

using System.ComponentModel;
using System.Data;
namespace Ark.Tools.Nodatime.Dapper;

public sealed class LocalDateTimeHandler : SqlMapper.TypeHandler<LocalDateTime>
{
    private LocalDateTimeHandler()
    {
    }

    public static readonly LocalDateTimeHandler Instance = new();

    [SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
    public event EventHandler<IDbDataParameter>? OnSetValue;

    public override void SetValue(IDbDataParameter parameter, LocalDateTime value)
    {
        parameter.Value = value.ToDateTimeUnspecified();
        //DbType could throw depending on the Driver implementation
        // see: https://community.oracle.com/tech/developers/discussion/2502273/converting-to-odp-net-oracletype-cursor-converts-to-dbtype
        //parameter.DbType = DbType.DateTime;

        OnSetValue?.Invoke(this, parameter);
    }

    public override LocalDateTime Parse(object? value)
    {
        if (value == null || value is DBNull) return default;

        if (value is DateTime dateTime)
        {
            return LocalDateTime.FromDateTime(dateTime);
        }

        if (value is string s)
        {
#if NET9_0_OR_GREATER
            // Use the trim-safe API available in .NET 9+
            var conv = TypeDescriptor.GetConverterFromRegisteredType(typeof(LocalDateTime));
#else
            // For .NET 8, suppress the warning as NodaTime TypeConverters are statically registered
            [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
                Justification = "The TypeConverter for NodaTime.LocalDateTime is statically registered in Ark.Tools.Nodatime and will not be trimmed. The LocalDateTime type is a known NodaTime struct with a well-defined TypeConverter.")]
            static TypeConverter GetConverter() => TypeDescriptor.GetConverter(typeof(LocalDateTime));
            var conv = GetConverter();
#endif
            if (conv?.CanConvertFrom(typeof(string)) == true)
                return (LocalDateTime)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime"));
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime");
    }
}
using Dapper;

using NodaTime;

using System;
using System.ComponentModel;
using System.Data;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Nodatime.Dapper(net10.0)', Before:
namespace Ark.Tools.Nodatime.Dapper
{
    public sealed class LocalDateTimeHandler : SqlMapper.TypeHandler<LocalDateTime>
    {
        private LocalDateTimeHandler()
        {
        }

        public static readonly LocalDateTimeHandler Instance = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
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
                var conv = TypeDescriptor.GetConverter(typeof(LocalDateTime));
                if (conv?.CanConvertFrom(typeof(string)) == true)
                    return (LocalDateTime)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime"));
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime");
        }
=======
namespace Ark.Tools.Nodatime.Dapper;

public sealed class LocalDateTimeHandler : SqlMapper.TypeHandler<LocalDateTime>
{
    private LocalDateTimeHandler()
    {
    }

    public static readonly LocalDateTimeHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
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
            var conv = TypeDescriptor.GetConverter(typeof(LocalDateTime));
            if (conv?.CanConvertFrom(typeof(string)) == true)
                return (LocalDateTime)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime"));
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime");
>>>>>>> After


namespace Ark.Tools.Nodatime.Dapper;

public sealed class LocalDateTimeHandler : SqlMapper.TypeHandler<LocalDateTime>
{
    private LocalDateTimeHandler()
    {
    }

    public static readonly LocalDateTimeHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
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
            var conv = TypeDescriptor.GetConverter(typeof(LocalDateTime));
            if (conv?.CanConvertFrom(typeof(string)) == true)
                return (LocalDateTime)(conv.ConvertFromString(s) ?? throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime"));
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.LocalDateTime");
    }
}
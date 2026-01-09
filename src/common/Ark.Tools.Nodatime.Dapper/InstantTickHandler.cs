using Dapper;

using NodaTime;

using System;
using System.Data;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Nodatime.Dapper(net10.0)', Before:
namespace Ark.Tools.Nodatime.Dapper
{
    public sealed class InstantTickHandler : SqlMapper.TypeHandler<Instant>
    {
        private InstantTickHandler()
        {
        }

        public static readonly InstantTickHandler Instance = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
        public event EventHandler<IDbDataParameter>? OnSetValue;

        public override void SetValue(IDbDataParameter parameter, Instant value)
        {
            parameter.Value = value.ToUnixTimeTicks();
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
                return Instant.FromUnixTimeTicks(l);
            }

            throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant");
        }
=======
namespace Ark.Tools.Nodatime.Dapper;

public sealed class InstantTickHandler : SqlMapper.TypeHandler<Instant>
{
    private InstantTickHandler()
    {
    }

    public static readonly InstantTickHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
    public event EventHandler<IDbDataParameter>? OnSetValue;

    public override void SetValue(IDbDataParameter parameter, Instant value)
    {
        parameter.Value = value.ToUnixTimeTicks();
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
            return Instant.FromUnixTimeTicks(l);
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant");
>>>>>>> After


namespace Ark.Tools.Nodatime.Dapper;

public sealed class InstantTickHandler : SqlMapper.TypeHandler<Instant>
{
    private InstantTickHandler()
    {
    }

    public static readonly InstantTickHandler Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "Historical mistake - Public API - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Historical mistake - Public API - Next Major")]
    public event EventHandler<IDbDataParameter>? OnSetValue;

    public override void SetValue(IDbDataParameter parameter, Instant value)
    {
        parameter.Value = value.ToUnixTimeTicks();
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
            return Instant.FromUnixTimeTicks(l);
        }

        throw new DataException("Cannot convert " + value.GetType() + " to NodaTime.Instant");
    }
}
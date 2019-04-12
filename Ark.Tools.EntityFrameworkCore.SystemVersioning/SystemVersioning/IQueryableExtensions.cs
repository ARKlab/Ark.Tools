using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using NodaTime;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	public static class IQueryableExtensions
	{
		internal static readonly MethodInfo SqlServerAsOfMethodInfo
		  = typeof(IQueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(SqlServerAsOf));

		public static IQueryable<TEntity> SqlServerAsOf<TEntity>(this IQueryable<TEntity> source, [NotParameterized] Instant asOf) 
			where TEntity : class
		{
			return
			  source.Provider is EntityQueryProvider
				? source.Provider.CreateQuery<TEntity>(
				  Expression.Call(
					null,
					SqlServerAsOfMethodInfo.MakeGenericMethod(typeof(TEntity)),
					source.Expression,
					Expression.Constant(asOf, typeof(Instant))
					))
				: source;
		}

		internal static readonly MethodInfo SqlServerBetweenMethodInfo
			= typeof(IQueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(SqlServerBetween));

		public static IQueryable<TEntity> SqlServerBetween<TEntity>(this IQueryable<TEntity> source, [NotParameterized] Instant startTime, [NotParameterized] Instant endTime) 
			where TEntity : class
		{
			return
			  source.Provider is EntityQueryProvider
				? source.Provider.CreateQuery<TEntity>(
				  Expression.Call(
					null,
					SqlServerBetweenMethodInfo.MakeGenericMethod(typeof(TEntity)),
					source.Expression,
					Expression.Constant(startTime, typeof(Instant)),
					Expression.Constant(endTime, typeof(Instant))
					))
				: source;
		}
	}
}

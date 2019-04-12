using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.Sql;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.Generators
{
	internal class SqlServerSystemVersioningQuerySqlGeneratorFactory : QuerySqlGeneratorFactoryBase
	{
		private readonly ISqlServerOptions _sqlServerOptions;

		public SqlServerSystemVersioningQuerySqlGeneratorFactory(
		  QuerySqlGeneratorDependencies dependencies,
		  ISqlServerOptions sqlServerOptions) : base(dependencies)
		{
			_sqlServerOptions = sqlServerOptions;
		}

		public override IQuerySqlGenerator CreateDefault(SelectExpression selectExpression)
		  => new SqlServerSystemVersioningQuerySqlGenerator(
			Dependencies,
			selectExpression,
			_sqlServerOptions.RowNumberPagingEnabled);
	}
}

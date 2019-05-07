using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.Sql;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Sql.Internal;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using Ark.Tools.EntityFrameworkCore.SystemVersioning.ExpressionsOverride;
using System.Reflection;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.Generators
{
	internal class SqlServerSystemVersioningQuerySqlGenerator : SqlServerQuerySqlGenerator
	{
		public SqlServerSystemVersioningQuerySqlGenerator(
		  QuerySqlGeneratorDependencies dependencies,
		  SelectExpression selectExpression,
		  bool rowNumberPagingEnabled)
		  : base(dependencies, selectExpression, rowNumberPagingEnabled)
		{
		}

		private static readonly FieldInfo _accessPrivateFieldBlameMe =
			typeof(DefaultQuerySqlGenerator).GetField("_relationalCommandBuilder", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic);

		public override Expression VisitTable(TableExpression tableExpression)
		{

			if (tableExpression is AsOfTableExpression asOfTableExppression)
			{
				var relationalCommandBuilder = (IRelationalCommandBuilder)_accessPrivateFieldBlameMe.GetValue(this);

				relationalCommandBuilder
					.Append(SqlGenerator.DelimitIdentifier(tableExpression.Table, tableExpression.Schema))
					.Append(" FOR SYSTEM_TIME AS OF '" + asOfTableExppression.AsOf.ToString() + "' ")
					.Append(AliasSeparator)
					.Append(SqlGenerator.DelimitIdentifier(tableExpression.Alias));

				return tableExpression;
			}
			else if (tableExpression is BetweenTableExpression betweenTableExpression)
			{
				var relationalCommandBuilder = (IRelationalCommandBuilder)_accessPrivateFieldBlameMe.GetValue(this);

				relationalCommandBuilder
					.Append(SqlGenerator.DelimitIdentifier(tableExpression.Table, tableExpression.Schema))
					.Append(" FOR SYSTEM_TIME BETWEEN '" + betweenTableExpression.StartTime.ToString() 
						  + "' AND '" + betweenTableExpression.EndTime.ToString() + "' ")
					.Append(AliasSeparator)
					.Append(SqlGenerator.DelimitIdentifier(tableExpression.Alias));

				return tableExpression;
			}
			else
			{
				return base.VisitTable(tableExpression);
			}
		}
	}
}

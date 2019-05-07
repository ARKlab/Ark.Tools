using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Expressions;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.ExpressionsOverride
{
	internal class SqlServerSystemVersioningSelectExpressionFactory : SelectExpressionFactory
	{
		public SqlServerSystemVersioningSelectExpressionFactory(SelectExpressionDependencies dependencies)
		  : base(dependencies)
		{
		}

		public override SelectExpression Create(RelationalQueryCompilationContext queryCompilationContext)
		  => new SqlServerSystemVersioningSelectExpression(Dependencies, queryCompilationContext);

		public override SelectExpression Create(RelationalQueryCompilationContext queryCompilationContext, string alias)
		  => new SqlServerSystemVersioningSelectExpression(Dependencies, queryCompilationContext, alias);
	}
}

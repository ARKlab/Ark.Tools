using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using NodaTime;
using System.Linq;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.ExpressionsOverride
{
	internal class SqlServerSystemVersioningSelectExpression : SelectExpression
	{
		public bool UseAsOf { get; set; }
		public Instant AsOf { get; set; }

		public bool UseBetween { get; set; }
		public Instant StartTime { get; set; }
		public Instant EndTime { get; set; }

		public SqlServerSystemVersioningSelectExpression(
		  SelectExpressionDependencies dependencies,
		  RelationalQueryCompilationContext queryCompilationContext) 
			: base(dependencies, queryCompilationContext)
		{
			SetCustomSelectExpressionProperties(queryCompilationContext);
		}

		public SqlServerSystemVersioningSelectExpression(
		  SelectExpressionDependencies dependencies,
		  RelationalQueryCompilationContext queryCompilationContext,
		  string alias) : base(dependencies, queryCompilationContext, alias)
		{
			SetCustomSelectExpressionProperties(queryCompilationContext);
		}

		public override void AddTable(TableExpressionBase tableExpression)
		{
			if (UseAsOf && tableExpression is TableExpression te)
				base.AddTable(new AsOfTableExpression(AsOf, te.Table, te.Schema, te.Alias, te.QuerySource));
			else if (UseBetween && tableExpression is TableExpression tableExpr)
				base.AddTable(new BetweenTableExpression(StartTime, EndTime, tableExpr.Table, tableExpr.Schema, tableExpr.Alias, tableExpr.QuerySource));
			else
				base.AddTable(tableExpression);

		}

		private void SetCustomSelectExpressionProperties(RelationalQueryCompilationContext queryCompilationContext)
		{
			if (queryCompilationContext.QueryAnnotations.Any(a => a.GetType() == typeof(AsOfResultOperator)))
			{
				UseAsOf = true;

				var asOfAnnotation = queryCompilationContext.QueryAnnotations.OfType<AsOfResultOperator>().LastOrDefault();

				AsOf = asOfAnnotation.AsOf;
			}
			else if (queryCompilationContext.QueryAnnotations.Any(a => a.GetType() == typeof(BetweenResultOperator)))
			{
				UseBetween = true;

				var bewtweenAnnotation = queryCompilationContext.QueryAnnotations.OfType<BetweenResultOperator>().LastOrDefault();

				StartTime = bewtweenAnnotation.StartTime;
				EndTime = bewtweenAnnotation.EndTime;
			}
		}
	}
}

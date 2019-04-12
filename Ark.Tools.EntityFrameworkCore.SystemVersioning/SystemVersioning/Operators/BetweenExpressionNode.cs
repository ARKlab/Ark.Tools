using NodaTime;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	internal class BetweenExpressionNode : ResultOperatorExpressionNodeBase
	{
		public static readonly IReadOnlyCollection<MethodInfo> SupportedMethods = new[] { IQueryableExtensions.SqlServerBetweenMethodInfo };

		private readonly ConstantExpression _expressionStart;
		private readonly ConstantExpression _expressionEnd;

		public BetweenExpressionNode(MethodCallExpressionParseInfo parseInfo, ConstantExpression expressionStart, ConstantExpression expressionEnd)
		  : base(parseInfo, null, null)
		{
			_expressionStart = expressionStart;
			_expressionEnd = expressionEnd;
		}

		protected override ResultOperatorBase CreateResultOperator(ClauseGenerationContext clauseGenerationContext)
		  => new BetweenResultOperator((Instant)_expressionStart.Value, (Instant)_expressionEnd.Value);

		public override Expression Resolve(
		  ParameterExpression inputParameter,
		  Expression expressionToBeResolved,
		  ClauseGenerationContext clauseGenerationContext)
		  => Source.Resolve(inputParameter, expressionToBeResolved, clauseGenerationContext);
	}
}

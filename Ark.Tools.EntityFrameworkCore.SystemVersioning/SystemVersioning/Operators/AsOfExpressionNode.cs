using NodaTime;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	internal class AsOfExpressionNode : ResultOperatorExpressionNodeBase
	{
		public static readonly IReadOnlyCollection<MethodInfo> SupportedMethods = new[] { IQueryableExtensions.SqlServerAsOfMethodInfo };

		private readonly ConstantExpression _asOfExpression;

		public AsOfExpressionNode(MethodCallExpressionParseInfo parseInfo, ConstantExpression asOfExpression)
		  : base(parseInfo, null, null)
		{
			_asOfExpression = asOfExpression;
		}

		protected override ResultOperatorBase CreateResultOperator(ClauseGenerationContext clauseGenerationContext)
		  => new AsOfResultOperator((Instant)_asOfExpression.Value);

		public override Expression Resolve(
		  ParameterExpression inputParameter,
		  Expression expressionToBeResolved,
		  ClauseGenerationContext clauseGenerationContext)
		  => Source.Resolve(inputParameter, expressionToBeResolved, clauseGenerationContext);
	}
}

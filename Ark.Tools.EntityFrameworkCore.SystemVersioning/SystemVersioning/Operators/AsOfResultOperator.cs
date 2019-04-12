using Microsoft.EntityFrameworkCore.Query.ResultOperators;
using NodaTime;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;
using System;
using System.Linq.Expressions;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	internal class AsOfResultOperator : SequenceTypePreservingResultOperatorBase, IQueryAnnotation
	{
		public virtual Instant AsOf { get; }
		public IQuerySource QuerySource { get; set; }
		public QueryModel QueryModel { get; set; }

		public AsOfResultOperator(Instant asOf)
		{
			AsOf = asOf;
		}

		public override ResultOperatorBase Clone(CloneContext cloneContext)
		  => new AsOfResultOperator(AsOf);

		public override StreamedSequence ExecuteInMemory<T>(StreamedSequence input) => input;

		public override void TransformExpressions(Func<Expression, Expression> transformation)
		{
		}
	}
}

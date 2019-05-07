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
	internal class BetweenResultOperator : SequenceTypePreservingResultOperatorBase, IQueryAnnotation
	{
		public virtual Instant StartTime { get; }
		public virtual Instant EndTime { get; }
		public IQuerySource QuerySource { get; set; }
		public QueryModel QueryModel { get; set; }

		public BetweenResultOperator(Instant startTime, Instant endTime)
		{
			StartTime = startTime;
			EndTime = endTime;
		}

		public override ResultOperatorBase Clone(CloneContext cloneContext)
		  => new BetweenResultOperator(StartTime, EndTime);

		public override StreamedSequence ExecuteInMemory<T>(StreamedSequence input) => input;

		public override void TransformExpressions(Func<Expression, Expression> transformation)
		{
		}
	}
}

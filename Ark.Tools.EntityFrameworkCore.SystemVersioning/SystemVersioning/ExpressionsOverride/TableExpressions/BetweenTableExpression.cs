using Microsoft.EntityFrameworkCore.Query.Expressions;
using NodaTime;
using Remotion.Linq.Clauses;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.ExpressionsOverride
{
	internal class BetweenTableExpression : TableExpression
	{
		public Instant StartTime { get; }
		public Instant EndTime { get; }

		public BetweenTableExpression(Instant startTime, Instant endTime, string table, string schema, string alias, IQuerySource querySource)
			: base(table, schema, alias, querySource)
		{
			StartTime = startTime;
			EndTime = endTime;
		}
	}
}

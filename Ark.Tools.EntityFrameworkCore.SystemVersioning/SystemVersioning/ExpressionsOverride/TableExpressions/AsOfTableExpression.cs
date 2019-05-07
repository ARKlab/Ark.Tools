using Microsoft.EntityFrameworkCore.Query.Expressions;
using NodaTime;
using Remotion.Linq.Clauses;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.ExpressionsOverride
{
	internal class AsOfTableExpression : TableExpression
	{
		public Instant AsOf { get; }

		public AsOfTableExpression(Instant asOf, string table, string schema, string alias, IQuerySource querySource)
			: base(table, schema, alias, querySource)
		{
			AsOf = asOf;
		}
	}
}

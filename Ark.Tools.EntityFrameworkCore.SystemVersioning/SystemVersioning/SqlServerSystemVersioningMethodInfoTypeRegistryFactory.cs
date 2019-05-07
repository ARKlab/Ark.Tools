using Microsoft.EntityFrameworkCore.Query.Internal;
using Remotion.Linq.Parsing.Structure;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	internal class SqlServerSystemVersioningMethodInfoTypeRegistryFactory : DefaultMethodInfoBasedNodeTypeRegistryFactory
	{
		public override INodeTypeProvider Create()
		{
			RegisterMethods(AsOfExpressionNode.SupportedMethods, typeof(AsOfExpressionNode));
			RegisterMethods(BetweenExpressionNode.SupportedMethods, typeof(BetweenExpressionNode));
			return base.Create();
		}
	}
}

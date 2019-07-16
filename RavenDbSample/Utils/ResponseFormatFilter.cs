using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;
using System.Linq;

namespace RavenDbSample.Utils
{
	public class ResponseFormatFilter : IOperationFilter
	{
		//public void Apply(Operation operation, OperationFilterContext context)
		//{
		//	if (operation.Produces != null)
		//	{
		//		foreach (var produce in operation.Produces)
		//		{
		//			if (produce == "application/json")
		//			{
		//				operation.Produces.Clear();
		//				operation.Produces.Add(produce);
		//				break;
		//			}
		//		}
		//	}

		//	if (operation.Consumes != null)
		//	{
		//		foreach (var consume in operation.Consumes)
		//		{
		//			if (consume == "application/json")
		//			{
		//				operation.Consumes.Clear();
		//				operation.Consumes.Add(consume);
		//				break;
		//			}
		//		}
		//	}
		//}


		public void Apply(Operation operation, OperationFilterContext context)
		{
			var whitelist = new[] { "application/json", "application/xml" };

			if (operation.Produces != null)
			{
				var toBeRemoved = operation.Produces.Except(whitelist).ToList();

				foreach (var item in toBeRemoved)
					operation.Produces.Remove(item);
			}

			if (operation.Consumes != null)
			{
				var toBeRemoved = operation.Consumes.Except(whitelist).ToList();

				foreach (var item in toBeRemoved)
					operation.Consumes.Remove(item);
			}
		}
	}
}

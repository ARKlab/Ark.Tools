using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;

namespace RavenDbSample.Utils
{
	public class ResponseFormatFilter : IOperationFilter
	{
		public void Apply(Operation operation, OperationFilterContext context)
		{
			if (operation.Produces != null)
			{
				foreach (var produce in operation.Produces)
				{
					if (produce == "application/json")
					{
						operation.Produces.Clear();
						operation.Produces.Add(produce);
						break;
					}
				}
			}

			if (operation.Consumes != null)
			{
				foreach (var consume in operation.Consumes)
				{
					if (consume == "application/json")
					{
						operation.Consumes.Clear();
						operation.Consumes.Add(consume);
						break;
					}
				}
			}
		}
	}
}

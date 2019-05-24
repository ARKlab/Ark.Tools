using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;

namespace RavenDbSample.Utils
{
	public class ResponseFormatFilter : IOperationFilter
	{
		public void Apply(Operation operation, OperationFilterContext context)
		{
			if (operation.Parameters == null)
				return;

			foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
			{
				foreach (var format in responseType.ApiResponseFormats)
				{
					if (format.MediaType == "application/json")
					{
						responseType.ApiResponseFormats.Clear();
						responseType.ApiResponseFormats.Add(format);
						break;
					}
				}
			}
		}
	}
}

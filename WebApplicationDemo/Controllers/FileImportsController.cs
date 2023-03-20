using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Ark.Tools.Solid;
using NLog;
using System.Threading;
using Ark.Tools.Core;
using NodaTime;
using WebApplicationDemo.Api.Requests;
using Asp.Versioning;

namespace WebApplicationDemo.Controllers
{
	[ApiVersion("1.0")]
	[Route("fileImports")]
	[ApiController]
	public class FileImportsController : ControllerBase
	{
		private static Logger _logger = LogManager.GetCurrentClassLogger();
		private readonly IQueryProcessor _queryProcessor;
		private readonly IRequestProcessor _requestProcessor;

		public FileImportsController(IQueryProcessor queryProcessor, IRequestProcessor requestProcessor)
		{
			_queryProcessor = queryProcessor;
			_requestProcessor = requestProcessor;
		}

		/// <summary>
		/// Create new Import
		/// </summary>
		/// <param name="file">file </param>
		/// <param name="file2">file2 </param>
		/// <param name="ctk">Cancellation Token</param>
		/// <returns></returns>
		[HttpPost()]
		public async Task<IActionResult> Post(IFormFile file, IFormFile file2, CancellationToken ctk = default)
		{
			_logger.Trace($@"UploadFile: { file.FileName }");

			if (file.Length > 0)
			{
				var request = new Post_FileImportRequest.V1()
				{
					FileName = file.FileName,
					File = file.OpenReadStream(),
				};

				var res = await _requestProcessor.ExecuteAsync(request, ctk);
				return Ok(res);
			}
			else
				return NoContent(); //????
		}

	}
}

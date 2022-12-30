using Ark.Tools.Solid;
using EnsureThat;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Http;
using NLog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using WebApplicationDemo.Api.Requests;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Application.Handlers.Requests
{
	public class Post_FileImportRequestHandler : IRequestHandler<Post_FileImportRequest.V1, FileImport?>
	{
		private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public FileImport? Execute(Post_FileImportRequest.V1 request)
		{
			return ExecuteAsync(request).GetAwaiter().GetResult();
		}

		public async Task<FileImport?> ExecuteAsync(Post_FileImportRequest.V1 request, CancellationToken ctk = default)
		{
			EnsureArg.IsNotNull(request, nameof(request));

			using (var buffer = new MemoryStream())
			{
				// Copy source to destination.
				request.File?.CopyTo(buffer);
				buffer.Seek(0, SeekOrigin.Begin);

				_logger.Info("FileImport_CreateRequestHandler - File Import created");

				var f = new FileImport()
				{
					FileName = request.FileName,
					ImportId = 10
				};

				return await Task.FromResult(f);
			}
		}

	}
}

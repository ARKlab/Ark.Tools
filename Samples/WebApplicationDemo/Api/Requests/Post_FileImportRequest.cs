using Ark.Tools.Solid;
using System.IO;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Requests
{
	public static class Post_FileImportRequest
	{
		public class V1 : IRequest<FileImport?>
		{
			public string? FileName { get; set; }
			public Stream? File { get; set; }
		}
	}
}

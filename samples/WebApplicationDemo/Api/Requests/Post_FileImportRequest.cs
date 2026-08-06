using Ark.Tools.Solid;


using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Requests;

public static class Post_FileImportRequest
{
    public class V1 : IRequest<V1, FileImport?>
    {
        public string? FileName { get; set; }
        public Stream? File { get; set; }
    }
}
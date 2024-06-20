
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Core.Service.WebInterface.Utils
{
    public class FormModel<T> : FileModel
    {
        [FromJson(Name = "Create")]
        public T Create { get; set; }
    }

    public class FileModel
    {
        [FromForm(Name = "File")]
        public IFormFile File { get; set; }
    }
}

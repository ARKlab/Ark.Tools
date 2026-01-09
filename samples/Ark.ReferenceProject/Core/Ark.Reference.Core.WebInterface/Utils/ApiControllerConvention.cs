using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Ark.Reference.Core.WebInterface.Utils
{
    internal sealed class ApiControllerConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            controller.Filters.Add(new ApiControllerAttribute());
        }
    }
}
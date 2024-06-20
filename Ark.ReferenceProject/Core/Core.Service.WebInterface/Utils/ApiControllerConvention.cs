using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Core.Service.WebInterface.Utils
{
    internal class ApiControllerConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            controller.Filters.Add(new ApiControllerAttribute());
        }
    }
}

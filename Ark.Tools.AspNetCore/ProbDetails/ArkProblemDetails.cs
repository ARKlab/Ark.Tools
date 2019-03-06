using Microsoft.AspNetCore.Mvc;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public abstract class ArkProblemDetails : Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        public ArkProblemDetails(string title)
        {
            Title = title;
        }
    }
}

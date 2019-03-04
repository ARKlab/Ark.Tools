using Microsoft.AspNetCore.Mvc;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public abstract class ArkProblemDetails : ProblemDetails
    {
        public ArkProblemDetails(string title)
        {
            Title = title;
        }
    }
}

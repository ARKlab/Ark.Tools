using Microsoft.AspNetCore.Mvc;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class ArkProblemDetails : ProblemDetails
    {
        public ArkProblemDetails(string title)
        {
            Title = title;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ark.Tools.AspNetCore.ProblemDetails
{
    public class ApiBehaviourOptionsSetup : IConfigureOptions<ApiBehaviorOptions>
    {
        public void Configure(ApiBehaviorOptions options)
        {
            options.SuppressMapClientErrors = true;
        }
    }
}
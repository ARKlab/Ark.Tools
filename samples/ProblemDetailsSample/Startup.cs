using Ark.Tools.AspNetCore.NestedStartup;
using Ark.Tools.AspNetCore.Startup;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace ProblemDetailsSample;

public class Startup : ArkStartupNestedRoot
{
    public Startup(IConfiguration configuration)
        : base(configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public override void Configure(IApplicationBuilder app)
    {
        base.Configure(app);

        app.UseBranchWithServices<PrivateStartup>("/private", Configuration);
    }
}
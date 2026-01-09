using Ark.Tools.AspNetCore.NestedStartup;
using Ark.Tools.AspNetCore.Startup;


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
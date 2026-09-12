using Ark.Tools.Compliance;


namespace LinuxWebJobHosting;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddArkRedaction();
    }

    public void Configure(IApplicationBuilder app)
    {
    }
}
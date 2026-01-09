using Ark.Tools.Core;


namespace WebApplicationDemo.Dto;

public class ExampleHealthCheckService : IExampleHealthCheckService
{
    public Task CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var healthCheckResultHealthy = true;

        if (healthCheckResultHealthy)
        {
            return Task.CompletedTask;
        }

        return Task.FromException(new OperationException("Failed"));
    }
}
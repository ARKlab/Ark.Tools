using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Hosting;


namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

/// <summary>
/// Hosted service that subscribes the <see cref="ResourceWatcherDiagnosticListener"/> to the
/// DiagnosticSource pipeline for the lifetime of the application.
/// </summary>
public sealed class ResourceWatcherTelemetryModule : IHostedService, IDisposable
{
    private readonly TelemetryConfiguration _configuration;
    private ResourceWatcherDiagnosticListener? _diagnosticListener;

    /// <summary>Initializes a new instance of <see cref="ResourceWatcherTelemetryModule"/>.</summary>
    /// <param name="configuration">The <see cref="TelemetryConfiguration"/> resolved from DI.</param>
    public ResourceWatcherTelemetryModule(TelemetryConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _diagnosticListener = new ResourceWatcherDiagnosticListener(_configuration);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _diagnosticListener?.Dispose();
    }
}
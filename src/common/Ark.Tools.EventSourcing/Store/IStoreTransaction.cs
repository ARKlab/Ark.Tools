using Ark.Tools.EventSourcing.Events;

using System.Security.Claims;

namespace Ark.Tools.EventSourcing.Store;

public interface IOperationContext
{
    string OperationId { get; }
    ClaimsPrincipal ExecutingPrincipal { get; }
    ClaimsPrincipal OnBehalfOfPrincipal { get; }

    IReadOnlyDictionary<string, string> Properties { get; }
    IReadOnlyDictionary<string, double> Metrics { get; }

    bool TrackMetric(string metric, double value, bool replaceExisting = true);
    bool TrackProperty(string property, string value, bool replaceExisting = true);
}

public interface IOperationContextFilter
{
    Task OnInit();
    Task OnBeforeCommit();
    Task OnAfterCommit();
}

public interface IDomainEventFilter
{
    void OnBeforeStore(DomainEventEnvelope @event);
}


public interface IStoreTransaction : IDisposable
{
    Task CommitAsync(IEnumerable<OutboxEvent> events, CancellationToken ctk = default);

}
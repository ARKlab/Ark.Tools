using Ark.Tools.EventSourcing.Events;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Store
{
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
=======
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
>>>>>>> After


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
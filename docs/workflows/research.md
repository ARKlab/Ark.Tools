# Embedded workflow engines for ASP.NET Core

**Research snapshot:** 2026-08-11
**Scope:** .NET 8/10, ASP.NET Core, Azure App Service, Azure Service Bus, Azure
Storage, and Azure SQL

## Executive decision

The requirement is not “run background jobs”. It is durable orchestration:
state must survive process restarts, activities may run for months, external
events must resume an idle instance, parallel work needs a safe join, and
operators need searchable state in infrastructure already owned by the company.

No candidate satisfies every requirement without a trade-off.

| Candidate | In-process ASP.NET Core | Existing SQL state | External wait | Durable fork/join | Azure fit | Architecture decision |
| --- | --- | --- | --- | --- | --- | --- |
| **Elsa 3** | Yes | Yes, EF Core providers | Yes, bookmarks | Yes, `Fork`/`WaitAll` | Good; Service Bus extension | **First embedded POC** |
| **Durable Task SDK** | Yes | No; Scheduler backend | Yes | Yes | Excellent, but adds Scheduler | **First Azure-native POC; cost/HA gate** |
| **Durable Functions isolated + MSSQL** | Separate Function App (can share an App Service plan) | Yes | Yes | Yes | Excellent | **Use when a Functions boundary is acceptable** |
| **Temporal .NET** | Worker can be hosted in ASP.NET Core | No; Temporal service owns history | Yes, signals/updates | Yes | Good, but external platform | **Use for platform-grade durable execution** |
| **Workflow Core** | Yes | Yes | Yes | Yes | Partial | **Only if maintenance risk is accepted** |
| **Dapr Workflow** | App plus Dapr sidecar | Via Dapr state component | Yes | Yes | Good, but adopts Dapr | **Use only with an existing Dapr platform** |
| **Rebus saga** | Yes | Yes | Messages | Manual counter/OCC | Excellent | **Good building block for a custom thin engine** |
| **MassTransit state machine** | Yes | Yes | Messages | Composite events/manual | Excellent | **Good if MassTransit is already standard** |
| **Wolverine saga** | Yes | Yes | Messages/timeouts | Manual | Excellent | **Good if Wolverine/Marten is already standard** |
| **Hangfire** | Yes | Yes | No native primitive | Pro batch only | Good | **Job scheduler, not the workflow engine** |
| **Quartz.NET** | Yes | Yes | No native primitive | No | Good | **Scheduler only** |
| **Orleans** | Yes | Grain state providers | Grain calls/streams | Custom grains | Good | **Use only with an existing Orleans platform** |
| **Stateless** | Yes | Caller-owned | Caller-owned | No | Neutral | **State-machine primitive only** |
| **Azure Logic Apps Standard** | No; separate resource | Platform-owned Storage | Yes | Yes | Excellent | **Low-code integration, not embedded code-first** |
| **Service Bus + custom state machine** | Yes | Caller-owned | Caller-owned | Caller-owned | Excellent | **Maximum control; highest engineering cost** |

### Recommended shortlist

Run a time-boxed proof of concept with:

1. **Elsa 3** for the strongest match to “embedded, SQL-backed, browsable,
   code-first, human-in-the-loop”.
2. **Durable Task SDK + Durable Task Scheduler** for the strongest Azure
   durable-execution model when a managed scheduler is acceptable.
3. **Durable Functions isolated + MSSQL provider** when SQL querying is more
   important than in-process hosting.
4. **Rebus saga + SQL outbox** only if Rebus is already the company messaging
   standard and the platform team is willing to own the missing workflow
   primitives.

Do not select Hangfire, Quartz.NET, or Stateless as the primary durable
workflow engine. They can be useful components in a larger design.

## Requirements and terminology

### Embedded means three different things

These deployment models must not be conflated:

1. **In-process:** the workflow worker and ASP.NET Core endpoints run in the
   same process. Elsa, Workflow Core, Rebus, MassTransit, Wolverine, and the
   standalone Durable Task SDK support this model.
2. **Co-resident:** separate resources share an App Service plan. A Durable
   Functions app can share a Dedicated App Service plan with a web app, but it
   remains a separate Function App.
3. **External execution platform:** the ASP.NET Core app is a client or worker
   connected to another service. Temporal and Durable Task Scheduler use this
   model for durable history and task dispatch.

The decision should explicitly choose the model. “Same Azure plan” is not the
same as “same application”.

### Capability interpretation

- **Code-first:** the workflow graph is authored in C# rather than only in a
  designer or JSON document.
- **State storage:** durable instance state, execution history, bookmarks,
  timers, and correlation data can be persisted.
- **Search/browse:** there is an API or queryable projection for operators and
  application endpoints. A dashboard alone is not a domain search API.
- **Fork/join:** branches are persisted and the join completes exactly once
  after the required branches complete.
- **External wait:** execution releases compute and resumes from a correlated
  user action, message, webhook, or timer.

## Capability matrix

| Capability | Durable Task / Functions | Elsa 3 | Temporal | Workflow Core | Dapr | Rebus/MassTransit/Wolverine | Orleans | Hangfire | Quartz | Stateless |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Code-first C# | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Lambdas/jobs | Jobs | Yes |
| In-process worker | SDK: yes; Functions: no | Yes | Yes, hosted worker | Yes | No, sidecar | Yes | Yes | Yes | Yes | Yes |
| Pluggable state store | Functions: several; SDK: Scheduler only | EF Core and extensions | Service-owned | Provider packages | Dapr components | Provider-specific | Grain providers | Storage providers | ADO.NET | Caller-owned |
| Existing Azure SQL | MSSQL provider; not standalone SDK | EF Core | No | Yes | Component | Yes | Yes | Yes | Yes | Yes |
| Searchable workflow state | MSSQL/SDK APIs; Storage is limited | REST APIs and SQL | Visibility API / Elasticsearch | Limited; optional Elasticsearch | Management API | Build a projection | Build a projection | Dashboard | Build it | Build it |
| Durable external event | `WaitForExternalEventAsync` | Bookmarks/stimulus | Signals/updates | `WaitFor`/`PublishEvent` | `WaitForExternalEventAsync` | Correlated messages | Grain calls/streams | No | No | No |
| Durable timer | Yes | Yes | Yes | Yes | Yes | Deferred/scheduled messages | Reminders | Scheduled jobs | Triggers | No |
| Fan-out/fan-in | `WhenAll` | `Fork`/`WaitAll` | `WhenAll` | `Parallel`/`Join` | `WhenAll` | Manual/composite event | Custom grains | Pro batches | No | No |
| Automatic retry | Activity retry policies | Resilience module | Activity retry policies | Per-step retry | Retry policy | Bus/saga retry | Application-owned | Yes | Misfire/retry | No |
| Compensation | Manual/Saga | Not a first-class shipped primitive | Manual saga | Saga compensation | Manual | Manual | Application-owned | No | No | No |
| Built-in workflow versioning | Yes | Definition versions | Patches/worker versions | `Id` + `Version` | Replay compatibility only | Manual | Grain/interface evolution | No | Job keys only | No |

“Yes” in this table means that the capability exists; it does not guarantee
that the candidate provides the required query model, concurrency guarantees,
or operational maturity for the company’s workload.

## Candidate evaluations

### 1. Elsa Workflows 3

**Profile:** MIT-licensed .NET workflow library, embedded through
`AddElsa`. Elsa 3 is the current stable line in this research snapshot. Elsa 4
is a roadmap concept, not a released migration target.

#### Hosting and persistence

Elsa runs as hosted services and middleware inside an ASP.NET Core application.
Its EF Core persistence is split into management and runtime concerns:

- definitions, publications, and versions;
- instances, bookmarks, triggers, execution records, and logs.

SQL Server, PostgreSQL, MySQL, Oracle, and SQLite providers are available in the
EF Core family. The REST API supports browsing definitions, instances,
bookmarks, execution logs, and activity records. The application can expose
domain-specific search endpoints over the same database.

Illustrative registration:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.AddWorkflow<ApprovalWorkflow>()
        .UseWorkflowManagement(management =>
            management.UseEntityFrameworkCore(ef => ef.UseSqlServer()))
        .UseWorkflowRuntime(runtime =>
            runtime.UseEntityFrameworkCore(ef => ef.UseSqlServer()))
        .UseWorkflowsApi()
        .UseScheduling();
});

var app = builder.Build();
app.MapWorkflowsApi("workflows");
app.UseWorkflows();
```

The exact registration surface must be pinned to the selected Elsa 3 package
minor version; the project has a next-generation persistence tree in
development.

#### Code-first, fork/join, and user action

```csharp
public sealed class ApprovalWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new("/approvals/start"),
                    SupportedMethods = new(new[] { HttpMethods.Post }),
                    CanStartWorkflow = true
                },
                new Fork
                {
                    JoinMode = ForkJoinMode.WaitAll,
                    Branches =
                    {
                        new SendEmail { /* approver A */ },
                        new SendEmail { /* approver B */ }
                    }
                },
                new Event { /* approval bookmark */ },
                new WriteHttpResponse { Content = new("Approved") }
            }
        };
    }
}
```

`ForkJoinMode.WaitAll` persists child completion and completes the parent only
after every branch is done. Blocking activities create bookmarks; an HTTP
callback, Service Bus trigger, or custom application endpoint stimulates the
matching bookmark. A dedicated “human task” activity is not required: the
application owns the approval record and authorization, while Elsa owns the
durable suspension and resume.

Elsa Extensions includes Azure Service Bus activities. Treat the extension as a
transport integration, not as the workflow state store.

#### Failure and evolution

Elsa uses incidents and incident strategies. The default can halt a workflow;
`ContinueWithIncidentsStrategy` records an incident and continues. Its
resilience module provides pluggable activity retries and transient exception
detection. Interrupted workflow recovery is handled by recurring runtime tasks.

Published definitions are versioned and active instances retain the definition
version on which they started. New instances use the latest published version.
Use this rule:

1. publish a new definition rather than editing a published definition;
2. keep input/output contracts additive;
3. migrate long-running instances explicitly when the business change requires
   it;
4. pin all Elsa Core, persistence, and extension packages to one tested minor
   line.

**Risks:** the persistence VNext work and the Elsa 4 roadmap can introduce
future migration work; a POC must test clustered startup, Service Bus
redelivery, SQL migrations, and query latency before adoption.

Sources:

- [Elsa Core](https://github.com/elsa-workflows/elsa-core)
- [Elsa Studio](https://github.com/elsa-workflows/elsa-studio)
- [Elsa Extensions](https://github.com/elsa-workflows/elsa-extensions)
- [Elsa documentation](https://docs.elsaworkflows.io/)
- [Elsa EF migrations](https://docs.elsaworkflows.io/guides/persistence/ef-migrations)
- [Elsa blocking activities](https://docs.elsaworkflows.io/activities/blocking-and-triggers)
- [Elsa workflow patterns](https://docs.elsaworkflows.io/guides/patterns)

### 2. Microsoft Durable Task and Durable Functions

Microsoft’s durable ecosystem has two useful hosting models:

1. **Durable Functions isolated worker:** Azure Functions hosts the orchestrator.
   It is not in-process with an existing ASP.NET Core app, although a Function
   App can share a Dedicated App Service plan.
2. **Standalone Durable Task SDK:** an ASP.NET Core process hosts the worker and
   client directly. The current managed path connects to Durable Task
   Scheduler, not to an arbitrary SQL database.

The .NET in-process Functions model reaches end of support on **2026-11-10**;
new work should use isolated worker.

#### Standalone ASP.NET Core hosting

The package names and registration API must be checked against the selected
stable SDK release. The pattern is:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDurableTaskClient(client => client.UseGrpc());
builder.Services.AddDurableTaskWorker(worker =>
{
    worker.AddTasks(registry =>
    {
        registry.AddOrchestrator<ApprovalOrchestrator>();
        registry.AddActivity<NotifyApproverActivity>();
    });
    worker.UseGrpc();
});

var app = builder.Build();

app.MapPost("/approvals", async (
    DurableTaskClient client,
    ApprovalRequest request) =>
{
    var instanceId =
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ApprovalOrchestrator), request);
    return Results.Accepted($"/orchestrations/{instanceId}", instanceId);
});

await app.RunAsync();
```

The standalone SDK uses Durable Task Scheduler as its durable backend. This
solves process embedding and provides a managed durable execution service, but
does **not** solve the requirement for arbitrary state tables in an existing
Azure SQL database. The scheduler dashboard and APIs are the supported
visibility surface.

#### Code-first fork/join and approval

```csharp
public sealed class ApprovalOrchestrator
    : TaskOrchestrator<ApprovalRequest, ApprovalResult>
{
    public override async Task<ApprovalResult> RunAsync(
        TaskOrchestrationContext context,
        ApprovalRequest request)
    {
        var notifications = new[]
        {
            context.CallActivityAsync(
                nameof(NotifyApproverActivity), request.PrimaryApprover),
            context.CallActivityAsync(
                nameof(NotifyApproverActivity), request.BackupApprover)
        };

        await Task.WhenAll(notifications);

        var approval = context.WaitForExternalEventAsync<bool>("Approval");
        var timeout = context.CreateTimer(
            context.CurrentUtcDateTime.AddDays(7));
        var completed = await Task.WhenAny(approval, timeout);

        if (completed == approval && await approval)
        {
            return new ApprovalResult(true);
        }

        await context.CallActivityAsync(
            nameof(EscalateApprovalActivity), request);
        return new ApprovalResult(false);
    }
}
```

Use the orchestration context’s deterministic clock and APIs. Never call
`DateTime.UtcNow`, random generators, network clients, or database code from
orchestrator code. Side effects belong in activities.

Durable Functions exposes the same primitives through
`TaskOrchestrationContext`, `DurableTaskClient`, `WaitForExternalEventAsync`,
durable timers, retry policies, sub-orchestrations, and entities. A Service Bus
trigger can call `RaiseEventAsync` to resume an instance.

#### Storage choices

| Provider | Strength | Main limitation |
| --- | --- | --- |
| Durable Task Scheduler | Managed, push dispatch, dashboard, managed identity | Standalone SDK is tied to it; not a queryable company SQL schema |
| Azure Storage | Cheap queues/tables/blobs, mature | Limited relational search and reporting |
| MSSQL provider | Azure SQL/on-prem SQL, ACID, backup/restore, SQL queries | Separate Functions hosting model; validate feature coverage |
| Netherite | High throughput | Retirement planned for 2028; do not start new work |

The MSSQL provider is the closest answer to the concern about tracking
orchestration state in Azure SQL. It should be evaluated separately from the
standalone SDK because storage-provider support differs by hosting model.

#### Versioning

Durable orchestrators replay history. Breaking changes include changing an
activity name or contract, or adding/removing/reordering durable calls, timers,
sub-orchestrations, or external events. Use orchestration versioning, or deploy
side-by-side task hubs/storage. Do not silently replace an orchestrator used by
in-flight instances.

Sources:

- [Durable Functions overview](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-overview)
- [Durable Task SDK overview](https://learn.microsoft.com/en-us/azure/durable-task/sdks/durable-task-overview)
- [Choose a hosting model](https://learn.microsoft.com/en-us/azure/durable-task/common/choose-orchestration-framework)
- [Storage providers](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-storage-providers)
- [Durable Task Scheduler](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler)
- [Fan-out/fan-in](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-fan-in-fan-out)
- [External events](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-external-events)
- [Code constraints](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-code-constraints)
- [Versioning](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-versioning)
- [Durable Task MSSQL provider](https://microsoft.github.io/durabletask-mssql)
- [Durable Task .NET SDK](https://github.com/microsoft/durabletask-dotnet)

#### Durable Task Scheduler production and cost gate

The Scheduler is a separate billable backend. Its price is in addition to the
ASP.NET Core/App Service or Functions compute plan, storage, messaging, and
observability costs. The official billing documentation currently describes two
SKUs:

| SKU | Billing and limits | Production reliability decision |
| --- | --- | --- |
| **Consumption** | Pay per dispatched action; up to 500 actions/second; up to 30 days of retention; no base capacity commitment | **Do not use as the production HA/multi-zone baseline.** HA is not available, and the official billing documentation labels this SKU as preview; verify status before procurement. |
| **Dedicated** | Fixed monthly price per capacity unit (CU); up to 2,000 actions/second and 50 GB of orchestration data per CU; up to 90 days of retention | HA requires **three CUs**. One or two CUs provide throughput but not Scheduler redundancy. |

For Dedicated, the minimum production HA topology is therefore three CUs. The
user-provided planning assumption of approximately **€500 per CU per month**
means approximately **€1,500/month for the Scheduler alone**. This is an
indicative regional estimate, not a price guarantee: validate currency, region,
taxes, retention, and negotiated discounts with the [Azure Functions pricing
page](https://azure.microsoft.com/pricing/details/functions/) before approval.
The application compute plan and any zone-redundant storage remain additional
costs.

Consumption is attractive for development, bursty non-critical workloads, and
cost experiments because the scheduler charge is proportional to action count.
It is not an acceptable substitute for multi-zone production reliability. A
three-activity orchestration can consume approximately seven actions (start,
activity dispatches/results, and completion), so the estimate must count
dispatches rather than business-level workflow instances:

```text
monthly Scheduler Consumption cost
    = (monthly dispatched actions / 1,000,000)
      × regional price per million actions
```

The standalone SDK has no arbitrary Azure SQL or Azure Storage backend; it is
coupled to the Scheduler. If the €1,500/month HA floor or the managed state
boundary is unacceptable, use Durable Functions with a BYO provider, or an
embedded SQL-backed engine such as Elsa, instead of weakening the reliability
requirement by selecting a one-CU Scheduler.

Sources:

- [Durable Task Scheduler billing](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler-billing)
- [Durable Task Scheduler](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler)
- [Durable Functions billing](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-billing)
- [Azure Functions pricing](https://azure.microsoft.com/pricing/details/functions/)

### 3. Temporal .NET SDK

Temporal is durable execution delivered as a service. A .NET worker can run
inside the ASP.NET Core Generic Host, but the history, task queues, and
visibility belong to a Temporal Server or Temporal Cloud deployment.

#### Hosting

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddTemporalClient("temporal.internal:7233", "company")
    .AddHostedTemporalWorker("orders")
    .AddWorkflow<OrderWorkflow>()
    .AddScopedActivities<OrderActivities>();

var app = builder.Build();

app.MapPost("/orders", async (
    ITemporalClient client,
    OrderRequest request) =>
{
    var handle = await client.StartWorkflowAsync(
        (OrderWorkflow workflow) => workflow.RunAsync(request),
        new(id: $"order-{request.OrderId}", taskQueue: "orders"));
    return Results.Accepted($"/orders/{handle.Id}", handle.Id);
});

await app.RunAsync();
```

Workflows are replayed from event history and must be deterministic. Activities
are the DI-enabled boundary for database, HTTP, Service Bus, and other side
effects. Temporal Cloud removes cluster operations. Self-hosting generally
requires Temporal services plus PostgreSQL/MySQL/Cassandra and optional
Elasticsearch/OpenSearch visibility.

#### Code-first signals and fork/join

```csharp
[Workflow]
public sealed class OrderWorkflow
{
    private bool _approved;

    [WorkflowRun]
    public async Task RunAsync(OrderRequest request)
    {
        var tasks = request.Items.Select(item =>
            Workflow.ExecuteActivityAsync(
                (OrderActivities activities) =>
                    activities.ProcessAsync(item),
                new ActivityOptions
                {
                    StartToCloseTimeout = TimeSpan.FromMinutes(5)
                }));

        await Workflow.WhenAllAsync(tasks);
        await Workflow.WaitConditionAsync(
            () => _approved,
            TimeSpan.FromDays(7));
    }

    [WorkflowSignal]
    public async Task ApproveAsync()
    {
        _approved = true;
        await Task.CompletedTask;
    }
}
```

The web API calls `SignalAsync` for a user action. Queries expose read-only
workflow state; updates provide a validated request/response operation. Activity
retry policies, heartbeats, child workflows, cancellation, and compensation
patterns cover resumability.

#### Evolution

Changing the sequence of workflow commands can cause
`WorkflowNondeterminismException`. Use `Workflow.Patched("patch-id")` through a
four-stage rollout: introduce the old/new branch, drain old histories,
deprecate the patch, then remove it. Worker deployment versioning can route
tasks to compatible workers. Continue-as-new bounds unbounded event history.
Replay production histories in CI before retiring a patch.

**Fit:** strongest execution semantics and visibility, but not a small embedded
library. It is a platform decision, not a NuGet-only dependency.

Sources:

- [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet)
- [Temporal .NET samples](https://github.com/temporalio/samples-dotnet)
- [Workflow basics](https://docs.temporal.io/develop/dotnet/workflows/basics)
- [Message passing](https://docs.temporal.io/develop/dotnet/workflows/message-passing)
- [Versioning](https://docs.temporal.io/develop/dotnet/workflows/versioning)
- [Continue-as-new](https://docs.temporal.io/develop/dotnet/workflows/continue-as-new)

### 4. Workflow Core

Workflow Core is a small, code-first engine with provider packages for SQL
Server, PostgreSQL, MongoDB, Cosmos DB, Redis, and other stores. Its fluent DSL
directly expresses wait, parallel, join, and compensation.

```csharp
public sealed class ApprovalWorkflow : IWorkflow<ApprovalData>
{
    public string Id => "Approval";
    public int Version => 2;

    public void Build(IWorkflowBuilder<ApprovalData> builder)
    {
        builder
            .StartWith<NotifyApprover>()
            .WaitFor("ApprovalReceived", data => data.RequestId)
            .Parallel()
                .Do(branch => branch.StartWith<AuditApproval>())
                .Do(branch => branch.StartWith<ProvisionOrder>())
            .Join()
            .Then<CompleteApproval>();
    }
}
```

The `WaitFor`/`PublishEvent` pair persists an idle workflow. `OnError` supports
retry, and saga blocks can register compensation. SQL Server persistence
reuses an existing database. An optional Elasticsearch provider can index
workflow state, but query and operational tooling are less complete than Elsa.

`Id` plus `Version` allows multiple definitions to coexist; new instances use
the selected/latest version and existing instances retain their version.
Persisted data and step contracts still need additive evolution.

**Risk:** repository activity appears substantially lower than the other
shortlisted engines. This is suitable for a controlled internal workload only
after an ownership, security patch, and clustered-concurrency review.

Sources:

- [Workflow Core repository](https://github.com/danielgerlag/workflow-core)
- [Getting started](https://workflow-core.readthedocs.io/en/latest/getting-started/)
- [Persistence](https://workflow-core.readthedocs.io/en/latest/persistence/)
- [External events](https://workflow-core.readthedocs.io/en/latest/external-events/)
- [Samples](https://workflow-core.readthedocs.io/en/latest/samples/)

### 5. Dapr Workflow

Dapr Workflow provides durable orchestration through the Dapr runtime. It is
not purely in-process: the application needs a Dapr sidecar. State stores can
be Azure Blob Storage, Cosmos DB, SQL Server, PostgreSQL, Redis, or another
Dapr-supported component; Azure Service Bus is a separate pub/sub component.

```csharp
public sealed class OrderWorkflow : Workflow<OrderRequest, OrderResult>
{
    public override async Task<OrderResult> RunAsync(
        WorkflowContext context,
        OrderRequest request)
    {
        var inventory = await context.CallActivityAsync<InventoryResult>(
            nameof(ReserveInventory),
            request,
            new WorkflowTaskOptions
            {
                RetryPolicy = new WorkflowRetryPolicy(
                    maxNumberOfAttempts: 3,
                    firstRetryInterval: TimeSpan.FromSeconds(5))
            });

        var approval = await context.WaitForExternalEventAsync<bool>(
            "ManagerApproval", TimeSpan.FromDays(7));

        return new OrderResult(inventory.Success && approval);
    }
}
```

Register workflow and activities with `AddDaprWorkflow`. Use
`WaitForExternalEventAsync`, `Task.WhenAll`, retry policies, and the Dapr
workflow management API. Code changes must remain replay-compatible; there is
no equivalent of Workflow Core’s integer version protocol.

**Fit:** compelling where Dapr is already the company runtime. Adopting Dapr
only for workflows adds a sidecar, state components, local tooling, and
operational ownership that conflicts with the “small embedded library” goal.

Sources:

- [Dapr Workflow](https://docs.dapr.io/developing-applications/building-blocks/workflow/)
- [Dapr .NET SDK examples](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow)

### 6. Orleans

Microsoft Orleans is an embeddable virtual-actor runtime, not a workflow
engine. A silo can run in an ASP.NET Core host, and grains can persist state in
Azure Storage, Azure SQL, Cosmos DB, or another provider. Calls, streams, and
reminders provide useful building blocks for external events and delayed work.

```csharp
public interface IOrderWorkflowGrain : IGrainWithStringKey
{
    Task StartAsync(OrderRequest request);
    Task ApproveAsync(string approvalId);
}

public sealed class OrderWorkflowGrain : Grain, IOrderWorkflowGrain
{
    private readonly IPersistentState<OrderState> _state;

    public OrderWorkflowGrain(
        [PersistentState("order", "workflowStore")]
        IPersistentState<OrderState> state)
    {
        _state = state;
    }

    public async Task StartAsync(OrderRequest request)
    {
        _state.State = new OrderState(request.OrderId, "AwaitingApproval");
        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(string approvalId)
    {
        if (_state.State.Status != "AwaitingApproval")
        {
            return;
        }

        _state.State = _state.State with { Status = "Approved" };
        await _state.WriteStateAsync();
    }
}
```

Orleans does not provide a first-class durable workflow graph, replay-safe
fork/join, human-task inbox, or workflow version protocol. Implementing a
parallel join requires child grains, idempotent completion records, and a
single-owner or optimistic-concurrency rule. Reminders are durable scheduling
primitives, not a substitute for a workflow history or searchable projection.
Version grain interfaces and persisted state explicitly; keep old serializers
and migration code while active grains drain.

**Cost and fit:** there is no workflow license fee, but the silo cluster,
membership, monitoring, and chosen grain-state store are additional operational
responsibilities. Orleans is a reasonable low-incremental-cost option only when
the company already operates Orleans. Introducing it solely for workflows has
more platform surface than Elsa and does not avoid custom orchestration work.

Sources:

- [Orleans overview](https://learn.microsoft.com/en-us/dotnet/orleans/overview)
- [Orleans ASP.NET Core hosting](https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/aspnet-host)
- [Orleans grain persistence](https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-persistence/)
- [Orleans reminders](https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders)

### 7. Stateless

Stateless is an excellent in-process state-machine primitive, not a durable
workflow engine. It has no storage, scheduler, retry policy, durable timer,
fork/join, or event inbox.

```csharp
var machine = new StateMachine<OrderState, OrderTrigger>(
    () => order.State,
    state => order.State = state);

machine.Configure(OrderState.Pending)
    .Permit(OrderTrigger.Submitted, OrderState.Submitted);

machine.Configure(OrderState.Submitted)
    .Permit(OrderTrigger.Approved, OrderState.Approved)
    .Permit(OrderTrigger.Rejected, OrderState.Rejected);

await machine.FireAsync(OrderTrigger.Submitted);
```

The application persists `order.State` and calls `FireAsync` after validating
the external event. Use an EF Core `rowversion` or equivalent concurrency
token. Version state and trigger contracts manually; never rename serialized
enum values without a migration.

**Fit:** useful inside a custom SQL-backed workflow aggregate or saga. It does
not remove the hard parts in this problem statement.

Source: [Stateless](https://github.com/dotnet-state-machine/stateless)

### 8. Hangfire

Hangfire is a persistent background-job system with SQL Server/Azure SQL
storage, retries, continuations, delayed jobs, and a dashboard. It is a useful
implementation substrate for simple workflows, but lacks a native external
event wait and workflow version protocol.

```csharp
var first = BackgroundJob.Enqueue(
    () => ProcessOrder(orderId));

BackgroundJob.ContinueJobWith(
    first,
    () => SendConfirmation(orderId));
```

Hangfire Pro batches provide a fan-out/fan-in continuation:

```csharp
var batchId = BatchJob.StartNew(batch =>
{
    batch.Enqueue(() => ProcessLine(line1));
    batch.Enqueue(() => ProcessLine(line2));
});

BatchJob.ContinueBatchWith(batchId, batch =>
    batch.Enqueue(() => JoinOrder(orderId)));
```

The batch API is a commercial Pro feature. Human approval requires a custom
database flag, polling job, or HTTP endpoint that enqueues the next job.
Serialized method identity makes renaming/moving methods a compatibility risk.
Use stable wrappers and versioned job arguments.

**Fit:** good for short-lived jobs and operational simplicity; insufficient
alone for durable human workflows.

Sources:

- [Hangfire](https://www.hangfire.io/)
- [ASP.NET Core integration](https://docs.hangfire.io/en/latest/getting-started/aspnet-core-applications.html)
- [SQL Server storage](https://docs.hangfire.io/en/latest/configuration/using-sql-server.html)
- [Hangfire batches](https://docs.hangfire.io/en/latest/background-methods/using-batches.html)

### 9. Quartz.NET

Quartz.NET is an embeddable clustered scheduler with persistent ADO.NET job
stores, cron expressions, calendars, misfire policies, and job recovery. It
does not model a workflow graph, durable external wait, or join.

```csharp
builder.Services.AddQuartz(options =>
{
    options.UsePersistentStore(store =>
        store.UseSqlServer(connectionString));
    options.AddJob<TimeoutJob>(
        job => job.WithIdentity("approval-timeout-v2"));
});
builder.Services.AddQuartzHostedService(options =>
    options.WaitForJobsToComplete = true);
```

Use a stable `JobKey` and versioned `JobDataMap` payloads. A new job key is the
safe path for incompatible code. Quartz is a good timer component for a custom
workflow aggregate or Elsa integration, not the aggregate itself.

Source: [Quartz.NET documentation](https://www.quartz-scheduler.net/)

### 10. Rebus sagas

Rebus is a message bus with sagas, SQL persistence, timeouts, and an outbox.
It fits Azure Service Bus and Azure SQL particularly well:

```csharp
public sealed class OrderSaga :
    Saga<OrderSagaData>,
    IAmInitiatedBy<OrderPlaced>,
    IHandleMessages<ApprovalReceived>
{
    protected override void CorrelateMessages(
        ICorrelationConfig<OrderSagaData> config)
    {
        config.Correlate<OrderPlaced>(m => m.OrderId, s => s.OrderId);
        config.Correlate<ApprovalReceived>(m => m.OrderId, s => s.OrderId);
    }

    public async Task Handle(OrderPlaced message)
    {
        Data.OrderId = message.OrderId;
        await Bus.Send(new ReserveOrder(message.OrderId));
    }

    public Task Handle(ApprovalReceived message)
    {
        MarkAsComplete();
        return Task.CompletedTask;
    }
}
```

`Rebus.SqlServer` supplies saga data/index tables, timeouts, subscriptions,
outbox, and optionally SQL transport. `Rebus.AzureServiceBus` supplies the
transport. Human actions are messages correlated by the business key.
Parallel work is manual: send one message per branch, store branch IDs, and
advance only when every completion has been recorded under optimistic
concurrency. There is no typed `Parallel().Join()` primitive.

Messages need explicit compatibility rules: add fields with defaults, preserve
old message handlers during drain, and use new message types for incompatible
contracts. Saga state migrations are application-owned.

Sources:

- [Rebus](https://github.com/rebus-org/Rebus)
- [Rebus SQL Server](https://github.com/rebus-org/Rebus.SqlServer)
- [Rebus Azure Service Bus](https://github.com/rebus-org/Rebus.AzureServiceBus)

### 11. MassTransit state machines

MassTransit combines a bus, consumers, and saga state machines. It supports
Azure Service Bus and SQL persistence through EF Core/Dapper and allows
optimistic or pessimistic saga concurrency.

```csharp
public sealed class OrderStateMachine :
    MassTransitStateMachine<OrderState>
{
    public State Submitted { get; private set; } = null!;
    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<LineCompleted> LineCompleted { get; private set; } = null!;
    public Event AllLinesCompleted { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);
        Event(() => OrderSubmitted,
            e => e.CorrelateById(m => m.Message.OrderId));

        CompositeEvent(
            () => AllLinesCompleted,
            state => state.CompletedLineStatus,
            LineCompleted);

        Initially(
            When(OrderSubmitted)
                .TransitionTo(Submitted));
        During(Submitted,
            When(AllLinesCompleted)
                .Finalize());
        SetCompletedWhenFinalized();
    }
}
```

`CompositeEvent` supplies fan-in after events arrive; fan-out and branch
tracking remain application design. Human actions are correlated messages and
timeouts are scheduled messages. Keep state names and serialized contracts
stable; add new states/events rather than renaming existing values.

**Fit:** strong if MassTransit already owns the company bus and saga
infrastructure. It is not a general durable workflow graph.

Sources:

- [MassTransit](https://github.com/MassTransit/MassTransit)
- [MassTransit saga state machines](https://masstransit.io/documentation/patterns/sagas/state-machine)
- [MassTransit Azure Service Bus](https://masstransit.io/documentation/transports/azure-service-bus)

### 12. Wolverine

Wolverine is a message bus and durable saga framework with Azure Service Bus,
SQL Server, PostgreSQL/Marten, inbox, outbox, retries, and scheduled messages.

```csharp
public sealed class Order : Saga
{
    public string Id { get; set; } = null!;

    public static (Order, OrderTimeout) Start(StartOrder command)
    {
        return (
            new Order { Id = command.OrderId },
            new OrderTimeout(command.OrderId));
    }

    public void Handle(ApprovalReceived message)
    {
        MarkCompleted();
    }
}

public sealed record OrderTimeout(string Id)
    : TimeoutMessage(TimeSpan.FromDays(1));
```

Fan-out is normally cascading messages, and fan-in is a state counter or
composite event implemented by the application. JSON saga state makes additive
properties relatively safe; renamed or retyped properties require a migration.
Wolverine 6 is a development line in this research snapshot; production
adoption should pin the stable line and follow its migration guide.

Source: [Wolverine](https://wolverinefx.net/)

### 13. Azure Logic Apps Standard

Logic Apps Standard is a separate Azure resource powered by the Functions
runtime. It supports stateful workflows, many workflows per app, connectors,
parallel branches, webhook callbacks, approvals, and a portal run history.
Stateful run data is stored in an associated Azure Storage account.

It cannot be embedded in an existing ASP.NET Core process or simply added as a
second application in the same ordinary App Service plan. It can be placed in
the same ASEv3 for network isolation, but remains a separate Logic App
deployment.

Logic Apps is a good choice for low-code integration and connector breadth. It
is not a code-first C# workflow engine and its portal history is not a
substitute for a company-owned domain query model.

Sources:

- [Logic Apps overview](https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-overview)
- [Standard versus Consumption](https://learn.microsoft.com/en-us/azure/logic-apps/single-tenant-overview-compare)
- [Logic Apps limits](https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-limits-and-config)

### 14. Azure messaging and storage primitives

Azure Service Bus, Storage Queues, Tables, Blobs, and SQL are useful building
blocks but are not workflow engines.

| Primitive | Useful workflow role | Missing capability |
| --- | --- | --- |
| Service Bus queues/topics | Reliable commands/events, sessions, DLQ, duplicate detection | No workflow state or join |
| Storage Queues | Cheap competing-consumer dispatch | No ordering, query, join, or external wait |
| Table Storage | Cheap instance/projection store | Partition-key query only; no full-text search |
| Blob Storage | Large payloads, documents, history archives | No orchestration |
| Azure SQL | Transactional state, rich search, reporting, OCC | No durable scheduler |

The Service Bus session ID can be the workflow correlation ID when strict
per-instance ordering is required. It does not make cross-instance fan-in
atomic. A transactional outbox and inbox are still required when SQL state and
Service Bus messages must move together.

Sources:

- [Service Bus overview](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview)
- [Service Bus duplicate detection](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection)
- [Compare Azure messaging services](https://learn.microsoft.com/en-us/azure/service-bus-messaging/compare-messaging-services)
- [Storage Queues](https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction)
- [Table Storage](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview)
- [Azure SQL overview](https://learn.microsoft.com/en-us/azure/azure-sql/database/sql-database-paas-overview)

## Cost and production-reliability comparison

Prices vary by Azure region, currency, commitment, tier, retention, and
throughput. The table intentionally compares billing surfaces rather than
inventing a universal monthly quote. Scheduler, compute, database, Service Bus,
storage, monitoring, and support costs are additive.

| Option | Scheduler/orchestration charge | Compute and state charge | Production reliability and operational cost |
| --- | --- | --- | --- |
| **Elsa 3 + existing ASP.NET Core** | No separate workflow license or scheduler service | Existing App Service plan; additional Azure SQL/Service Bus/Storage transactions and App Insights usage | Run multiple app instances, use HA SQL/Service Bus, and own clustered recovery, upgrades, and engine support |
| **Workflow Core + existing ASP.NET Core** | No separate scheduler service | Existing plan plus selected SQL provider and message/storage costs | Similar HA design, with materially higher maintenance and security-patch ownership risk |
| **Durable Task SDK + Scheduler Consumption** | Pay per dispatched action; 500 actions/sec ceiling; 30-day retention | Existing App Service compute; Scheduler state is managed | **Not a production multi-zone baseline:** no Scheduler HA and currently documented as preview; low idle cost does not compensate for the reliability gap |
| **Durable Task SDK + Scheduler Dedicated** | Fixed per-CU price; HA requires three CUs | Existing App Service compute; managed Scheduler state | **Indicative minimum Scheduler HA cost: ~€1,500/month** at the requested ~€500/CU assumption, before compute, storage, messaging, monitoring, tax, and support |
| **Durable Functions isolated + Azure Storage provider** | No Scheduler fee | Function App can share an existing Dedicated App Service plan; pay Storage queue/table/blob transactions | Separate Function App; use ZRS/GZRS for state and zone-redundant compute where required; 16-node orchestration scale-out ceiling and limited SQL search |
| **Durable Functions isolated + MSSQL provider** | No Scheduler fee | Function App plus existing Azure SQL capacity/storage and query workload | Separate Function App; SQL HA and backups are strong; validate provider feature coverage, especially Durable Entities in isolated worker |
| **Temporal Cloud** | Vendor usage/retention/service charges | Existing app/worker compute; no self-managed Temporal cluster | Lowest platform-operations burden but external vendor cost, data boundary, and visibility contract; obtain a current quote |
| **Temporal self-hosted** | No vendor service fee | Temporal services plus PostgreSQL/MySQL/Cassandra and optional search, in addition to workers | Highest infrastructure and on-call cost; zone redundancy must be designed for every service |
| **Dapr Workflow** | No workflow-specific service fee | App plus sidecar, state component, and pub/sub; AKS or Container Apps may add platform cost | Reasonable only when Dapr is already operated; otherwise sidecars and control-plane ownership erase the apparent savings |
| **Orleans** | No workflow-specific service fee | Existing compute plus silo membership and grain-state provider | Low incremental bill only with an existing Orleans platform; custom fork/join, history, projections, and versioning create engineering cost |
| **Logic Apps Standard** | Fixed Workflow Service Plan, plus connector and storage charges | Separate Logic App resource and associated Storage account | Managed run history and connectors, but not in-process or code-first; separate resource/plan and possible integration-account costs |
| **Service Bus + custom SQL state machine** | No engine fee | Existing App Service, Azure SQL, and Service Bus tier/operations | Maximum cost control and reuse, but the company owns idempotency, timers, joins, inbox/outbox, migrations, dashboards, and 24x7 support |

### Normalized planning examples

Assume an existing Dedicated App Service plan, Azure SQL database, and Service
Bus namespace. For **10,000 workflows/month** with approximately **seven
Scheduler actions each**, the Scheduler Consumption estimate is:

```text
70,000 actions / 1,000,000 × regional price per million actions
```

That figure excludes the existing plan and all application dependencies, and it
still does **not** provide Scheduler HA. The Dedicated alternative is not
usage-priced: three CUs are the minimum HA topology. At the requested
indicative €500/CU/month, the Scheduler portion is approximately €1,500/month
even if the workload is idle. The one-CU alternative lowers the bill but is not
an acceptable zone-resilient production topology.

The lower-cost production paths are therefore:

1. **Elsa 3** or **Workflow Core** in the existing ASP.NET Core process when the
   team accepts application-owned clustered recovery and SQL projections.
2. **Durable Functions + Azure Storage** when a separate Function App is
   acceptable and limited relational search is sufficient.
3. **Durable Functions + MSSQL** when the existing Azure SQL resource is the
   most valuable asset and rich state queries outweigh in-process hosting.
4. **A custom Service Bus/SQL thin orchestration layer** only when the platform
   team budgets for the missing durable-execution features explicitly.

Do not compare the €1,500 Scheduler estimate with only a library's NuGet cost.
The correct comparison includes engineering ownership, incident response,
database capacity, HA topology, data retention, and the cost of migrating
long-running instances when a product or provider changes.

Sources:

- [Durable Task Scheduler billing](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler-billing)
- [Azure Functions pricing](https://azure.microsoft.com/pricing/details/functions/)
- [Azure App Service pricing](https://azure.microsoft.com/pricing/details/app-service/windows/)
- [Azure Functions zone redundancy](https://learn.microsoft.com/en-us/azure/azure-functions/functions-zone-redundancy)
- [Azure Functions reliability](https://learn.microsoft.com/en-us/azure/reliability/reliability-functions)
- [Durable Task storage providers](https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-storage-providers)
- [Durable Functions Azure Storage provider](https://learn.microsoft.com/en-us/azure/durable-task/durable-functions/durable-functions-azure-storage-provider)
- [Durable Task MSSQL provider](https://microsoft.github.io/durabletask-mssql)
- [Temporal Cloud pricing](https://temporal.io/pricing)
- [Azure Service Bus pricing](https://azure.microsoft.com/pricing/details/service-bus/)
- [Azure Logic Apps pricing](https://azure.microsoft.com/pricing/details/logic-apps/)

## Architecture patterns required regardless of engine

Selecting a library does not remove these platform requirements.

### 1. Separate execution state from read/search projections

Use the engine’s durable state as the source of truth, then maintain a
query-oriented projection:

```text
workflow instance + history
        │
        ├── instance projection: status, current step, owner, due date
        ├── task projection: assignee, action URL, approval state
        └── audit projection: immutable events and correlation IDs
```

Expose company APIs over the projection, not over engine-private tables. This
avoids coupling every consumer to a vendor schema and makes retention/PII
policies explicit.

### 2. Use optimistic concurrency for every state mutation

For Azure SQL, store a `rowversion` or an integer state version. The join,
external event, timeout, and retry paths must all use the same concurrency
fence:

```sql
UPDATE WorkflowInstances
SET State = @state,
    StateVersion = StateVersion + 1,
    UpdatedAt = SYSUTCDATETIME()
WHERE InstanceId = @instanceId
  AND StateVersion = @expectedVersion;
```

If zero rows are updated, reload and retry or return a conflict. Never read,
increment, and write a completion counter without a conditional update.

### 3. Make fork/join idempotent

Persist one branch row per `(instance_id, branch_id)` with a unique constraint.
On a completion message:

1. insert the branch completion idempotently;
2. increment or recalculate the completed count in the same transaction;
3. transition the parent only if all expected branch IDs exist;
4. enqueue the next activity through an outbox;
5. use a unique transition key so the join continuation is scheduled once.

This is safer than trusting delivery order or a volatile in-memory counter.
Cap fan-out by a deliberate concurrency limit; an unlimited `WhenAll` can
create a storage or downstream-service hot spot.

### 4. Correlate and buffer external events

Store a stable business correlation key and the engine instance ID. External
event records should include:

- instance ID and event name;
- external event ID / idempotency key;
- payload schema version;
- received timestamp and actor;
- processed timestamp and outcome.

If the engine supports durable event buffering, use it. For a custom engine,
insert the event before attempting to resume the instance so an event arriving
just before a wait cannot be lost.

### 5. Use an inbox/outbox boundary

Persist workflow state and an outbox message in one SQL transaction. A
background dispatcher publishes the message to Service Bus and records the
delivery. Consumers record the message ID in an inbox table under the same
transaction as their state change.

Service Bus duplicate detection is a useful second defence, not a replacement
for application idempotency. Azure SQL and Service Bus do not share a
distributed transaction.

Sources:

- [Transactional outbox with Cosmos DB](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos)
- [Azure Service Bus duplicate detection](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection)
- [Optimistic concurrency in Cosmos DB](https://learn.microsoft.com/en-us/azure/cosmos-db/database-transactions-optimistic-concurrency)

## Versioning and compatibility policy

Adopt these rules for every engine, even if the engine offers stronger
mechanisms.

### Workflow definition

1. Give every definition a stable logical name and immutable integer/string
   version.
2. New instances select a specific version at creation.
3. Existing instances stay on their original version unless an explicit,
   audited migration is performed.
4. Keep old activity names and deserializers until the maximum workflow
   lifetime plus replay/drain margin has elapsed.
5. Prefer side-by-side definitions over conditionals based on deployment time.

### Activity and message contracts

- Add optional fields with safe defaults.
- Do not change the meaning of an existing field.
- Do not change a serialized type in place; introduce `ThingV2`.
- Keep old readers during rolling deployment.
- Use tolerant deserialization and upcasters for event/history payloads.
- Give external events and messages a schema version.
- Never put secrets or PII in an event history when the engine retains history
  for the workflow lifetime.

### Deterministic replay engines

Durable Task, Durable Functions, Temporal, and Dapr replay workflow code.
Treat the workflow method as a pure program:

- use the engine clock and ID/random APIs;
- keep I/O in activities;
- keep collection ordering deterministic;
- do not change the sequence of durable calls for in-flight histories;
- add a patch/version gate before changing the path;
- replay representative production histories in CI.

### Rolling deployment sequence

```text
1. Deploy readers for old and new payloads.
2. Deploy new definition/version, leaving old workers available.
3. Route only new instances to the new version.
4. Observe old instances until drained or explicitly migrated.
5. Remove old code only after retention + replay safety window.
```

For a long-running workflow, “the deployment succeeded” is not evidence that
the old workflow is safe to delete.

## Evaluation plan

Before selecting a product, run the same scenario against Elsa, Durable Task,
and the chosen incumbent messaging option:

1. Start an instance and query it by business correlation ID.
2. Execute two branches concurrently and deliver one completion twice.
3. Crash the worker after one branch commits and before the outbox publishes.
4. Restart and prove the join runs once.
5. Suspend for a human approval, restart the app, approve through an HTTP API,
   and verify the instance resumes.
6. Deliver the approval before the wait point and verify buffering.
7. Retry a transient activity and route a permanent failure to an operator
   state/dead-letter path.
8. Deploy a compatible version while instances are running.
9. Deploy an incompatible version and prove the documented versioning path.
10. Search and browse state under realistic history volume and retention.
11. Scale to two or more workers and measure lock contention, duplicate work,
    and recovery time.
12. Verify managed identity, private networking, SQL migrations, and PII
    redaction.

Record throughput, p95 resume latency, recovery time, storage growth, operator
steps, and cost. A workflow engine that passes a happy-path demo but loses a
pre-wait event or double-runs a join is not acceptable.

## Final recommendation

Start with **Elsa 3** if direct in-process hosting and Azure SQL-backed
search/browsing are non-negotiable. Keep the application-facing API behind a
small company abstraction so the engine’s persisted schema is not exposed to
other services.

In parallel, validate **Durable Task SDK + Durable Task Scheduler** only if the
company accepts the managed state boundary and Scheduler cost. Consumption is
not a production multi-zone baseline; the Dedicated HA topology requires three
CUs and is approximately €1,500/month at the requested indicative
€500/CU/month assumption before other Azure costs. The SDK supports direct
ASP.NET Core hosting, but it trades arbitrary SQL state ownership for the
managed Scheduler backend.

If the company can accept a separate Function App, validate **Durable
Functions isolated + MSSQL provider**. It combines the Durable programming
model with Azure SQL persistence and rich SQL reporting, at the cost of a
hosting boundary and provider-specific limitations.

Choose **Temporal** only when the organization is willing to operate or buy a
durable execution platform. Choose **Workflow Core** only with explicit
ownership of maintenance risk. Choose **Orleans** only when its silo platform
is already standard; it is not a workflow engine by itself. Choose
**Rebus/MassTransit/Wolverine** when the messaging platform is already standard
and the team accepts implementing the workflow projection, join protocol, and
migration policy.

Do not build a bespoke engine until these three POCs fail a requirement that
cannot be solved with a small adapter or projection. If all three fail, build
only the missing orchestration layer over an existing SQL/outbox/messaging
stack; do not reimplement durable execution, retries, timers, and visibility
without a written operations budget.

## Source index

### Engines and products

- [Durable Task documentation hub](https://learn.microsoft.com/en-us/azure/durable-task/)
- [Durable Task .NET SDK](https://github.com/microsoft/durabletask-dotnet)
- [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet)
- [Elsa Core](https://github.com/elsa-workflows/elsa-core)
- [Workflow Core](https://github.com/danielgerlag/workflow-core)
- [Dapr Workflow](https://docs.dapr.io/developing-applications/building-blocks/workflow/)
- [Stateless](https://github.com/dotnet-state-machine/stateless)
- [Hangfire](https://github.com/HangfireIO/Hangfire)
- [Quartz.NET](https://github.com/quartznet/quartznet)
- [Rebus](https://github.com/rebus-org/Rebus)
- [MassTransit](https://github.com/MassTransit/MassTransit)
- [Wolverine](https://github.com/JasperFx/wolverine)
- [Azure Logic Apps](https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-overview)

### Azure infrastructure and architecture patterns

- [Azure Service Bus](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview)
- [Azure Storage Queues](https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction)
- [Azure Table Storage](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview)
- [Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/sql-database-paas-overview)
- [Event Sourcing pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
- [Saga pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga)
- [Compensating transaction pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction)
- [Transactional outbox](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos)
- [Azure Schema Registry](https://learn.microsoft.com/en-us/azure/event-hubs/schema-registry-overview)

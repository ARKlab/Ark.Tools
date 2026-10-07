# Sample hosting variants implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize `samples/Ark.MediatorFramework.Sample` into one `Core`
service (API, Application, Database, application tests) hosted by three
self-contained variants — `Web`, `WebRebus`, `Functions` — without changing the
shared parts per variant.

**Architecture:** The shared service lives under `Core/`. Each variant lives
under `Core/Hosts/<Variant>/` and owns one class library
(`…Core.<Variant>.Hosting`) with the composition shared by its processes, one
executable per messaging participant, and one host-boundary test project.
Application tests run on the native in-memory messaging transport and never
reference a host. The old layout is deleted as each variant replaces it.

**Tech Stack:** .NET 10, SimpleInjector, Ark.Tools.MediatorFramework (Minimal
API, gRPC, MCP, native messaging, Azure Functions, Rebus generators), Reqnroll +
MSTest + AwesomeAssertions, SQL Server DACPAC, Service Bus (emulator for tests).

**Spec:** [`docs/design/mediator-framework/sample-hosting-variants.md`](../../design/mediator-framework/sample-hosting-variants.md)

## Global Constraints

- Follow `AGENTS.md`: file header, file-scoped namespaces, Allman braces, CRLF,
  `var` for locals, XML docs on public members, `async`/`await` (never return
  a `Task` directly), `ConfigureAwait(false)`, NLog structured logging with
  `CultureInfo.InvariantCulture`, `AwesomeAssertions`, `TreatWarningsAsErrors`.
- Conventional Commits, summary ≤ 72 chars, lower case, imperative, no period;
  scope `samples` for sample work, `MediatorFramework` for framework work. End
  every commit message with `Assisted-by: Claude`. No `Co-authored-by`.
- No new third-party dependency. Every package used below is already in
  `samples/Ark.MediatorFramework.Sample/Directory.Packages.props`.
- No secrets in source. Connection strings come from configuration. The
  well-known local SQL emulator connection (from `docker-compose.yml`) goes
  only into `appsettings.Development.json` files and test configuration.
- Project names: `Ark.MediatorFramework.Sample.Core.<Layer>` for shared layers,
  `Ark.MediatorFramework.Sample.Core.<Variant>.<Role>` for host projects.
  Namespace = project name.
- One root solution: `samples/Ark.MediatorFramework.Sample/Ark.MediatorFramework.Sample.slnx`.
  Every project added or removed is also added to or removed from
  `Ark.Tools.slnx`.
- API and Application must not reference `Rebus*`, `Ark.Tools.Rebus`,
  `Ark.Tools.Outbox.Rebus`, `Ark.Tools.MediatorFramework.Rebus`,
  `Microsoft.Azure.Functions.*`, or `Microsoft.AspNetCore.*` (end state, Task 7).
- Each task ends green: `dotnet build Ark.Tools.slnx --configuration Debug`
  succeeds and the affected test projects pass. Run sample tests with
  `ARK_SAMPLE_INMEMORY_TESTS=1` when Docker is unavailable, and with the SQL
  profile (`docker compose -f samples/Ark.MediatorFramework.Sample/docker-compose.yml up -d db`)
  before pushing.
- Locked restore: `Ark.Tools.Sdk` sets `RestorePackagesWithLockFile=true` and
  CI restores with `RestoreLockedMode=true`. Every project created or whose
  references change in a task gets its `packages.lock.json` regenerated with
  `dotnet restore <project>` and committed in the same commit; moved projects
  keep theirs (`git mv` the folder). Before each commit run
  `dotnet restore Ark.Tools.slnx -p:RestoreLockedMode=true`; expected: success.
- `ArkApiSurface.txt` diffs are accepted only for the renames and topology
  changes this plan names. Accept with
  `dotnet build <project> -p:EmitCompilerGeneratedFiles=true` then copy
  `obj/Debug/net10.0/ArkApiSurface.current.txt` over `ArkApiSurface.txt`.

## Paths used below

```text
S  = samples/Ark.MediatorFramework.Sample
C  = S/Core
W  = C/Hosts/Web
WR = C/Hosts/WebRebus
F  = C/Hosts/Functions
```

Shell snippets assume the repository root as working directory and
`S=samples/Ark.MediatorFramework.Sample`.

## File structure (end state)

| Path | Responsibility |
| --- | --- |
| `C/…Core.API/` | Public contracts, transport metadata, API JSON context. |
| `C/…Core.Application/` | Handlers, validators, decorators, DAL, services, messages, participants, `ApplicationComposition`, `ApplicationOptions`. |
| `C/…Core.Database/` | SQL project and DACPAC. |
| `C/…Core.Tests/` | Reqnroll application scenarios; native in-memory participant harness. |
| `W/…Core.Web.Hosting/` | Web variant shared composition: container, native messaging participant registration, principal flow. |
| `W/…Core.Web.WebInterface/` | Minimal API + gRPC + MCP + OpenAPI host; Api participant producer. |
| `W/…Core.Web.Processor/`, `…NotificationProcessor/`, `…AuditProcessor/` | Native `MessagingProcessorHost` receivers, one participant each. |
| `W/…Core.Web.OutboxProcessor/` | Single `MessagingOutboxProcessor`. |
| `W/…Core.Web.GrpcClient/` | gRPC client generated from exported protos. |
| `W/…Core.Web.Tests/` | Web host-boundary tests. |
| `WR/…Core.WebRebus.Hosting/` | Rebus shared configuration (serializer, routing, user flow, outbox). |
| `WR/…Core.WebRebus.WebInterface/` | Minimal API host; Rebus one-way client. |
| `WR/…Core.WebRebus.Processor/`, `…NotificationProcessor/`, `…AuditProcessor/` | Rebus receivers, one `ArkRebusHost` each. |
| `WR/…Core.WebRebus.Tests/` | Rebus host-boundary tests. |
| `F/…Core.Functions.Hosting/` | `HttpHost` declaration, shared Functions composition, principal flow. |
| `F/…Core.Functions.Api/` | HTTP functions + native producer. |
| `F/…Core.Functions.Processor/`, `…Notifications/`, `…Audit/` | Generated Service Bus triggers, one participant each. |
| `F/…Core.Functions.OutboxProcessor/` | Always-running `MessagingOutboxProcessor`. |
| `F/…Core.Functions.Tests/` | Functions host-boundary tests. |
| `S/Ark.MediatorFramework.Sample.Core.<Variant>.yml` (+ `.buildStage.yml`, `.deployStage.yml`) | One Azure DevOps pipeline per variant. |

---

### Task 1: Expose pending-message count on the in-memory transport

Host-neutral application tests need a public way to wait until in-memory work
drains. Today only `GetDeadLetters` is public and the sample uses
`InternalsVisibleTo`.

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/InMemoryMessagingTransport.cs` (after `GetDeadLetters`, ~line 280)
- Test: `tests/Ark.Tools.MediatorFramework.Tests/InMemoryMessagingTransportPendingCountTests.cs`
- Modify: `CHANGELOG.md` (`## [Unreleased]` → `### Added`)

**Interfaces:**
- Produces: `public int InMemoryMessagingTransport.GetPendingCount(string queue)` — visible + scheduled + locked deliveries; `0` for an unknown queue.

- [ ] **Step 1: Write the failing test**

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class InMemoryMessagingTransportPendingCountTests
{
    private static readonly IReadOnlyDictionary<string, string> _headers =
        new Dictionary<string, string>(StringComparer.Ordinal);

    [TestMethod]
    public void UnknownQueueHasNoPendingMessages()
    {
        var transport = new InMemoryMessagingTransport();

        transport.GetPendingCount("missing").Should().Be(0);
    }

    [TestMethod]
    public async Task PendingCountIncludesVisibleScheduledAndLockedDeliveries()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.SendAsync("q", _headers, new ReadOnlySequence<byte>([1]), dueTime: null, default)
            .ConfigureAwait(false);
        await transport.SendAsync("q", _headers, new ReadOnlySequence<byte>([2]), DateTimeOffset.UtcNow.AddHours(1), default)
            .ConfigureAwait(false);

        transport.GetPendingCount("q").Should().Be(2, "one visible and one scheduled delivery are pending");

        var batch = await transport.ReceiveBatchAsync("q", 1, TimeSpan.Zero, default).ConfigureAwait(false);
        batch.Should().ContainSingle();
        transport.GetPendingCount("q").Should().Be(2, "the locked delivery and the scheduled delivery are pending");

        await batch[0].CompleteAsync(default).ConfigureAwait(false);
        transport.GetPendingCount("q").Should().Be(1, "only the scheduled delivery is pending");
    }
}
```

`SendAsync(string queue, IReadOnlyDictionary<string,string> headers, ReadOnlySequence<byte> payload, DateTimeOffset? dueTime, CancellationToken ctk)`
puts a future `dueTime` into the scheduled queue, so the second assertion fails
if `_scheduled.Count` is omitted and the third fails if `_locked.Count` is
omitted.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests --filter "FullyQualifiedName~InMemoryMessagingTransportPendingCountTests"`
Expected: build error `'InMemoryMessagingTransport' does not contain a definition for 'GetPendingCount'`.

- [ ] **Step 3: Implement**

Insert after `GetDeadLetters`:

```csharp
    /// <summary>Gets the number of deliveries that are not yet settled for a queue.</summary>
    /// <remarks>Counts visible, scheduled, and locked deliveries. Dead letters are excluded.</remarks>
    /// <param name="queue">The queue name.</param>
    /// <returns>The pending delivery count, or zero when the queue does not exist.</returns>
    public int GetPendingCount(string queue)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        lock (_gate)
        {
            return _queues.TryGetValue(queue, out var target)
                ? target._visible.Count + target._scheduled.Count + target._locked.Count
                : 0;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: same command as Step 2. Expected: 2 passed.

- [ ] **Step 5: Changelog**

Under `## [Unreleased]` → `### Added` add:

```markdown
- `InMemoryMessagingTransport.GetPendingCount` reports unsettled deliveries per queue, so tests can wait for in-memory messaging to drain.
```

- [ ] **Step 6: Commit**

```bash
git add src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/InMemoryMessagingTransport.cs \
  tests/Ark.Tools.MediatorFramework.Tests/InMemoryMessagingTransportPendingCountTests.cs CHANGELOG.md
git commit -m "feat(MediatorFramework): add in-memory transport pending count" -m "Assisted-by: Claude"
```

---

### Task 2: Move the shared projects into `Core/`

Pure move and rename. No behavior change. Old hosts and the old test project
stay where they are and follow the new references.

**Files:**
- Move: `S/src/Ark.MediatorFramework.Sample.API` → `C/Ark.MediatorFramework.Sample.Core.API`
- Move: `S/src/Ark.MediatorFramework.Sample.Application` → `C/Ark.MediatorFramework.Sample.Core.Application`
- Move: `S/src/Ark.MediatorFramework.Sample.Database` → `C/Ark.MediatorFramework.Sample.Core.Database`
- Modify: every `*.cs`, `*.csproj`, `*.sqlproj`, `*.slnx`, `*.feature`, `*.json`, `*.yml`, `*.md` under `S/` that names the moved projects
- Modify: `Ark.Tools.slnx`, `S/Ark.MediatorFramework.Sample.slnx`, `S/Directory.Build.props` (no change to `ArkExportProtoDir` yet)
- Modify: both `ArkApiSurface.txt` and `ArkComplianceSurface.txt` (namespace-only diff)

**Interfaces:**
- Produces: namespaces `Ark.MediatorFramework.Sample.Core.API`, `Ark.MediatorFramework.Sample.Core.Application.*`; DACPAC `Ark.MediatorFramework.Sample.Core.Database.dacpac`. The database name stays `Ark.MediatorFramework.Sample`.

- [ ] **Step 1: Move with history**

```bash
S=samples/Ark.MediatorFramework.Sample
mkdir -p $S/Core
git mv $S/src/Ark.MediatorFramework.Sample.API         $S/Core/Ark.MediatorFramework.Sample.Core.API
git mv $S/src/Ark.MediatorFramework.Sample.Application $S/Core/Ark.MediatorFramework.Sample.Core.Application
git mv $S/src/Ark.MediatorFramework.Sample.Database    $S/Core/Ark.MediatorFramework.Sample.Core.Database
for p in API Application; do
  git mv $S/Core/Ark.MediatorFramework.Sample.Core.$p/Ark.MediatorFramework.Sample.$p.csproj \
         $S/Core/Ark.MediatorFramework.Sample.Core.$p/Ark.MediatorFramework.Sample.Core.$p.csproj
done
git mv $S/Core/Ark.MediatorFramework.Sample.Core.Database/Ark.MediatorFramework.Sample.Database.sqlproj \
       $S/Core/Ark.MediatorFramework.Sample.Core.Database/Ark.MediatorFramework.Sample.Core.Database.sqlproj
```

- [ ] **Step 2: Rename identifiers**

```bash
grep -rlE 'Ark\.MediatorFramework\.Sample\.(API|Application|Database)\b' \
  $S Ark.Tools.slnx --include='*.cs' --include='*.csproj' --include='*.sqlproj' \
  --include='*.slnx' --include='*.props' --include='*.targets' --include='*.json' \
  --include='*.yml' --include='*.feature' --include='*.md' \
  | grep -v '/obj/\|/bin/' \
  | xargs sed -i -E 's/Ark\.MediatorFramework\.Sample\.(API|Application|Database)\b/Ark.MediatorFramework.Sample.Core.\1/g'
```

Then fix relative paths that the rename cannot know:

- In every moved `.csproj`, the generator reference
  `../../../../src/mediator-framework/…` keeps the same depth (`src/X` and
  `Core/X` are both two levels under `S`); verify it resolves.
- In the old hosts and old tests, replace
  `..\Ark.MediatorFramework.Sample.Core.API\…` with
  `..\..\Core\Ark.MediatorFramework.Sample.Core.API\Ark.MediatorFramework.Sample.Core.API.csproj`
  (same for Application, Database). From `S/test/*` use `..\..\Core\…`.
- In `Ark.Tools.slnx` and `S/Ark.MediatorFramework.Sample.slnx`, point the three
  entries at `samples/Ark.MediatorFramework.Sample/Core/…` (root) and
  `Core/…` (sample slnx). Add a solution folder `/Core/` in the sample slnx.
- In `S/test/Ark.MediatorFramework.Sample.Tests/Hooks/DatabaseHooks.cs`, the
  DACPAC file name becomes `Ark.MediatorFramework.Sample.Core.Database.dacpac`;
  keep the database name `"Ark.MediatorFramework.Sample"`.

- [ ] **Step 3: Build and accept the namespace-only surface diff**

```bash
dotnet build $S/Ark.MediatorFramework.Sample.slnx -p:EmitCompilerGeneratedFiles=true
```

Expected: `ARKAPI002` on API and Application. Inspect the diff of
`obj/Debug/net10.0/ArkApiSurface.current.txt` against `ArkApiSurface.txt`:
only namespace text may change. Copy current over committed for both
projects; repeat for `ArkComplianceSurface.txt` if its gate fails. Rebuild
without the property; expected: success.

- [ ] **Step 4: Run the existing tests**

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests
```

Expected: same pass count as before the move.

- [ ] **Step 5: Full build**

Run: `dotnet build Ark.Tools.slnx --configuration Debug`. Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add -A samples/Ark.MediatorFramework.Sample Ark.Tools.slnx
git commit -m "refactor(samples): move shared mediator sample projects into core" -m "Assisted-by: Claude"
```

---

### Task 3: Introduce `ApplicationOptions` and subscriber registration

Adds the final composition API next to the current one. Old hosts keep using
the current overload until they are deleted (Tasks 5–7).

**Files:**
- Create: `C/Ark.MediatorFramework.Sample.Core.Application/Host/ApplicationOptions.cs`
- Modify: `C/Ark.MediatorFramework.Sample.Core.Application/Host/ApplicationComposition.cs`
- Test: `S/test/Ark.MediatorFramework.Sample.Tests/ApplicationCompositionTests.cs` (moves to `Core.Tests` in Task 4)

**Interfaces:**
- Produces:
  - `public sealed record ApplicationOptions { string? SqlConnectionString; ISampleDataContextFactory? DataContextFactory; IClock Clock; IPrintCompletedNotificationService PrintCompletedNotificationService; }`
  - `public static void ApplicationComposition.Register(Container container, ApplicationOptions options)` — shared graph only: processors, persistence, clock, validators, handlers except `ICommandHandler<BookPrintCompleted>`, decorators. Registers no `IBus`, no `IContextProvider<ClaimsPrincipal>`, no sink.
  - `public static void ApplicationComposition.RegisterNotificationSubscriber(Container container, IBookPrintNotificationSink sink)`
  - `public static void ApplicationComposition.RegisterAuditSubscriber(Container container, IBookPrintAuditSink sink)`
- Temporary: the existing `Register(Container, bool, string?, IClock?, …)` overload stays and delegates; deleted in Task 7.

- [ ] **Step 1: Write the failing test**

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.Tests;

[TestClass]
public sealed class ApplicationCompositionTests
{
    [TestMethod]
    public void RegisterRequiresExactlyOnePersistenceSource()
    {
        using var container = _newContainer();

        var act = () => ApplicationComposition.Register(container, new ApplicationOptions());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SqlConnectionString*DataContextFactory*");
    }

    [TestMethod]
    public void RegisterRejectsBothPersistenceSources()
    {
        using var container = _newContainer();

        var act = () => ApplicationComposition.Register(container, new ApplicationOptions
        {
            SqlConnectionString = "Server=unused;Database=unused",
            DataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory()),
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SqlConnectionString*DataContextFactory*");
    }

    [TestMethod]
    public void SubscribersRegisterTheirOwnCompletedPrintHandler()
    {
        using var notification = _newContainer();
        using var audit = _newContainer();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());

        ApplicationComposition.Register(notification, new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterNotificationSubscriber(notification, new NoOpBookPrintNotificationSink());
        ApplicationComposition.Register(audit, new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterAuditSubscriber(audit, new NoOpBookPrintAuditSink());

        notification.GetRegistration(typeof(ICommandHandler<BookPrintCompleted>))!
            .ImplementationType.Should().Be<BookPrintNotificationHandler>();
        audit.GetRegistration(typeof(ICommandHandler<BookPrintCompleted>))!
            .ImplementationType.Should().Be<BookPrintAuditHandler>();
    }

    private static Container _newContainer()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        return container;
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests --filter "FullyQualifiedName~ApplicationCompositionTests"`
Expected: build error, `ApplicationOptions` not found.

- [ ] **Step 3: Create `ApplicationOptions`**

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Core.Application.Host;

/// <summary>Inputs for <see cref="ApplicationComposition.Register(SimpleInjector.Container, ApplicationOptions)"/>.</summary>
public sealed record ApplicationOptions
{
    /// <summary>Gets the SQL Server connection string. Set exactly one of this or <see cref="DataContextFactory"/>.</summary>
    [InfrastructureSecret]
    public string? SqlConnectionString { get; init; }

    /// <summary>Gets a context factory to use instead of SQL Server, for example the in-memory factory in tests.</summary>
    public ISampleDataContextFactory? DataContextFactory { get; init; }

    /// <summary>Gets the application clock.</summary>
    public IClock Clock { get; init; } = SystemClock.Instance;

    /// <summary>Gets the external print-completion adapter.</summary>
    public IPrintCompletedNotificationService PrintCompletedNotificationService { get; init; }
        = new NoOpPrintCompletedNotificationService();
}
```

- [ ] **Step 4: Add the new `Register` and subscriber methods**

In `ApplicationComposition.cs`, rename the body of the existing `Register(...)`
into a private `_registerShared(Container container, ApplicationOptions options)`
with these changes:

- persistence branch: `options.DataContextFactory` → register instance (and as
  `IOutboxAsyncContextFactory`); else `options.SqlConnectionString` → SQL
  branch using that string with **no** fallback connection string; if both or
  neither are set throw
  `new InvalidOperationException("Set exactly one of ApplicationOptions.SqlConnectionString or ApplicationOptions.DataContextFactory.")`.
- delete the hard-coded `SqlConnectionStringBuilder` block.
- clock: `container.RegisterInstance(options.Clock);`
- `container.RegisterInstance(options.PrintCompletedNotificationService);`
- do not register `IBookPrintNotificationSink`, `IBookPrintAuditSink`, or
  `ICommandHandler<BookPrintCompleted>`.

Then add:

```csharp
    /// <summary>Registers the shared application graph.</summary>
    /// <param name="container">The application container.</param>
    /// <param name="options">The persistence, clock, and external adapter choices.</param>
    public static void Register(Container container, ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(options);
        _registerShared(container, options);
    }

    /// <summary>Registers the notification subscriber's completed-print handler.</summary>
    /// <param name="container">The notification process container.</param>
    /// <param name="sink">The notification sink.</param>
    public static void RegisterNotificationSubscriber(Container container, IBookPrintNotificationSink sink)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(sink);
        container.RegisterInstance(sink);
        container.Register<ICommandHandler<BookPrintCompleted>, BookPrintNotificationHandler>();
    }

    /// <summary>Registers the audit subscriber's completed-print handler.</summary>
    /// <param name="container">The audit process container.</param>
    /// <param name="sink">The audit sink.</param>
    public static void RegisterAuditSubscriber(Container container, IBookPrintAuditSink sink)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(sink);
        container.RegisterInstance(sink);
        container.Register<ICommandHandler<BookPrintCompleted>, BookPrintAuditHandler>();
    }
```

Rewrite the existing overload as a temporary adapter (keep its XML docs and add
`/// <remarks>Temporary: removed when the legacy hosts are deleted.</remarks>`):

```csharp
    public static void Register(
        Container container,
        bool useSqlStore = true,
        [InfrastructureSecret] string? connectionString = null,
        IClock? clock = null,
        ISampleDataContextFactory? dataContextFactory = null,
        IPrintCompletedNotificationService? printCompletedNotificationService = null,
        bool registerBookPrintNotificationHandler = true,
        IBookPrintNotificationSink? bookPrintNotificationSink = null,
        IBookPrintAuditSink? bookPrintAuditSink = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        _registerShared(container, new ApplicationOptions
        {
            SqlConnectionString = dataContextFactory is null && useSqlStore
                ? connectionString ?? throw new InvalidOperationException("A SQL connection string is required.")
                : null,
            DataContextFactory = dataContextFactory
                ?? (useSqlStore ? null : new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory())),
            Clock = clock ?? SystemClock.Instance,
            PrintCompletedNotificationService = printCompletedNotificationService
                ?? new NoOpPrintCompletedNotificationService(),
        });
        container.RegisterInstance(bookPrintNotificationSink ?? new NoOpBookPrintNotificationSink());
        container.RegisterInstance(bookPrintAuditSink ?? new NoOpBookPrintAuditSink());
        if (registerBookPrintNotificationHandler)
            container.Register<ICommandHandler<BookPrintCompleted>, BookPrintNotificationHandler>();
    }
```

The legacy in-memory branch previously registered `InMemoryOutboxContextFactory`
as its own singleton; check the old hosts and tests for
`GetInstance<InMemoryOutboxContextFactory>()` and, if any exist, resolve
through `ISampleDataContextFactory` instead.

Callers that passed no connection string with `useSqlStore: true` relied on
the deleted fallback. Fix each one by passing the value from configuration:
the old web/processor `Program.cs` files read `ConnectionStrings:Sample`; the
old test project reads `DatabaseHooks.ConnectionString`.

- [ ] **Step 5: Run the new and the existing tests**

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests
```

Expected: all pass, including the 3 new tests.

- [ ] **Step 6: Full build and commit**

```bash
dotnet build Ark.Tools.slnx --configuration Debug
git add -A samples/Ark.MediatorFramework.Sample
git commit -m "refactor(samples): add application options and subscriber registration" -m "Assisted-by: Claude"
```

---

### Task 4: Host-neutral application tests (`Core.Tests`)

Create `Core.Tests` with the Reqnroll scenarios running all four participants on
`InMemoryMessagingTransport`, one SimpleInjector container per participant, as
real processes would. Remove the moved scenarios from the old test project.

**Files:**
- Create: `C/Ark.MediatorFramework.Sample.Core.Tests/Ark.MediatorFramework.Sample.Core.Tests.csproj`
- Move (git mv from `S/test/Ark.MediatorFramework.Sample.Tests/`): `Features/`, `Auth/` (`AuthTestContext` holds the `Given I am an authenticated user…` bindings the feature uses), `NativeOutboxIntegrationTests.cs` (minus `DedicatedHostResolvesExactlyOneReservedProcessor`, see Step 2), `Steps/BookSteps.cs`, `Steps/BookPrintingProcessSteps.cs`, `Drivers/`, `Fakes/`, `Init/`, `Hooks/HooksOrder.cs`, `Hooks/DatabaseHooks.cs`, `Hooks/SampleTestContext.cs`, `Hooks/ApplicationTestContext.cs`, `ApplicationTestContextTests.cs`, `ApplicationCompositionTests.cs`, `ConcurrencyRoundtripTests.cs`, `AsyncEnumerableStreamingTests.cs`, `BookStreamingAndEditionTests.cs`, `TestAssemblyConfiguration.cs`, `GlobalUsings.cs` (copy, keep original for old project)
- Create: `C/…Core.Tests/Hooks/InMemoryMessagingHarness.cs`
- Create: `C/…Core.Tests/Hooks/ParticipantProcess.cs`
- Create: `C/…Core.Tests/Hooks/BackgroundMessagingContext.cs` (replaces `RebusScenarioContext`)
- Create: `C/…Core.Tests/Steps/BackgroundMessagingSteps.cs` (replaces `RebusSteps`)
- Modify (Step 0, framework): `src/mediator-framework/Ark.Tools.MediatorFramework.Generators/MessagingNetworkGenerator.cs`, `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/{MessagingParticipantDescriptor,MessagingDispatcher,IMessagingPipelineProcessor,FluentMessagingComposition}.cs`, `src/mediator-framework/Ark.Tools.MediatorFramework.AzureFunctions/MessagingFunctionsServiceCollectionExtensions.cs` and the generated-trigger emitter if it calls the dispatch delegate, `AnalyzerReleases.Unshipped.md`, the analyzer rule docs under `docs/mediator-framework/`, `CHANGELOG.md`, API surface and generator snapshots the change moves
- Test (Step 0): `tests/Ark.Tools.MediatorFramework.Tests/` (generator compile test + runtime dispatch test)
- Delete from old project: `Hooks/ProcessWideApplicationTestFixture.cs`, `ProcessWideApplicationFixtureTests.cs` (the process-wide pattern is not carried forward), `Steps/RebusSteps.cs`, `Hooks/RebusScenarioContext.cs` (Rebus behavior moves to WebRebus tests in Task 6)
- Modify: `S/Ark.MediatorFramework.Sample.slnx`, `Ark.Tools.slnx`

**Interfaces:**
- Consumes: Task 1 `GetPendingCount`; Task 3 `ApplicationOptions`, `Register`, `RegisterNotificationSubscriber`, `RegisterAuditSubscriber`.
- Produces: native messaging dispatches `IRequest<TSelf, TResponse>` contracts listed in `Processes` (response discarded); `CreateBookReviewRequest.V1` is sent as-is by every host variant.
- Produces (test-only, used by later tasks as reference): `ParticipantProcess` (container + `ServiceProvider` + started `IHostedService`s for one participant), `InMemoryMessagingHarness.EnsureTopologyAsync(InMemoryMessagingTransport)`.

- [ ] **Step 0: Framework — requests as messages**

Ruling (user): a request is a valid *message* (sent to one processor); it is
not a valid *event*. Native messaging must honour that. Today the generator
breaks it in three ways, all fixed here in `Ark.Tools.MediatorFramework`, with
the Application unchanged (`SampleMessagingParticipant.Processes` keeps
`CreateBookReviewRequest.V1`):

1. `MessagingNetworkGenerator` emits dispatch cases only for `ICommand<TSelf>`
   (`.Where(contract => !canEmitBinder || _implementsCommand(...))`); a request
   in `Processes` gets no case and dead-letters as `UnknownContractName`.
2. Case bodies have no braces, so a participant processing two or more
   contracts redeclares `message`/`failed` in one switch scope (CS0128).
3. Nothing reports a `Processes`/`Subscribes` contract the binder cannot
   dispatch, and ARKMSG018 accepts `IRequest` for `[Event]` contracts.

Changes:

- Dispatch seam carries both processors. Add `IRequestProcessor` next to
  `ICommandProcessor` in `MessagingDispatch`, `MessagingFailedDispatch`,
  `MessagingDispatcher`'s delegate fields, and the
  `IMessagingPipelineProcessor.ProcessIncomingAsync` terminal
  (`Func<ICommandProcessor, IRequestProcessor, CancellationToken, Task>`); the
  service-provider pipeline resolves both from the scope. Update every caller
  (fluent receiver, Functions composition and generated triggers, tests). The
  framework is unreleased; record the signature change in the changelog.
- Generator, `DispatchAsync`: a `Processes` contract implementing
  `IRequest<TSelf, TResponse>` emits
  `await requestProcessor.ExecuteAsync<T, TResponse>(message, ctk).ConfigureAwait(false);`
  and discards the response. Commands are unchanged.
- `DispatchFailedAsync` and second-level retries: unchanged shape —
  `MessagingFailed<T>` is a command for any `T`, so requests use the command
  processor there.
- `HandlerServiceTypes`: emit `IRequestHandler<T, TResponse>` for requests
  (keep `ICommandHandler<MessagingFailed<T>>` when second-level retries are on).
- Wrap every emitted `case` body in braces.
- Diagnostics: a request in `Subscribes` or `Publishes`, or an `[Event]`
  contract implementing `IRequest`, is an error (tighten ARKMSG018 to
  `ICommand<TSelf>` only; if ARKMSG018 is listed in
  `AnalyzerReleases.Shipped.md`, add a new rule id instead of changing it).
  A `Processes` contract that is neither command nor request is an error with
  a new rule id. Register new ids in `AnalyzerReleases.Unshipped.md` and the
  analyzer rule docs.
- Investigate the CS1061 the implementer saw (`ConfigureAwait` on
  `MessagingStreamPayloadReader` in the stream binder): fix it if it is real,
  otherwise make the compile test reference what the binder needs.

Tests (write first, see them fail):
- Generator compile test, following `GeneratorSnapshotTests._runGeneratorDriver`
  + `RunGeneratorsAndUpdateCompilation`: a participant with
  `Processes = { SomeCommand, SomeRequest }` and second-level retries compiles
  with no errors, and `DispatchAsync` contains a request-processor call.
- Generator diagnostics tests: request in `Subscribes`; request `[Event]`;
  non-command/non-request in `Processes`.
- Runtime test on `InMemoryMessagingTransport`: a receiver whose participant
  processes a request runs its `IRequestHandler` once and settles the message.

CHANGELOG (`## [Unreleased]`): `Added` — native messaging processes request
contracts sent as messages; `Fixed` — participants processing several
contracts now compile, and undispatchable contracts are reported at build time.

Commit separately before the sample work:
`feat(MediatorFramework): dispatch requests sent as messages`.

- [ ] **Step 1: Project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SqlServer.DacFx" />
    <PackageReference Include="Moq" />
    <PackageReference Include="NodaTime.Testing" />
    <PackageReference Include="Reqnroll.MsTest" />
    <PackageReference Include="Reqnroll.Tools.MsBuild.Generation" />
    <PackageReference Include="Ark.Tools.Reqnroll" />
    <PackageReference Include="Ark.Tools.Solid.SimpleInjector" />
    <PackageReference Include="Ark.Tools.MediatorFramework.Messaging" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Ark.MediatorFramework.Sample.Core.API\Ark.MediatorFramework.Sample.Core.API.csproj" />
    <ProjectReference Include="..\Ark.MediatorFramework.Sample.Core.Application\Ark.MediatorFramework.Sample.Core.Application.csproj" />
    <ProjectReference Include="..\Ark.MediatorFramework.Sample.Core.Database\Ark.MediatorFramework.Sample.Core.Database.sqlproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <OutputItemType>Content</OutputItemType>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </ProjectReference>
    <None Include="..\Ark.MediatorFramework.Sample.Core.Database\bin\$(Configuration)\Ark.MediatorFramework.Sample.Core.Database.dacpac"
          Link="Ark.MediatorFramework.Sample.Core.Database.dacpac"
          CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

The `.sqlproj` metadata and the linked `.dacpac` item are the same wiring the
old test project uses, with the renamed paths, so `DatabaseHooks` finds
`Ark.MediatorFramework.Sample.Core.Database.dacpac` in the output directory.

- [ ] **Step 2: Move the files**

```bash
OLD=$S/test/Ark.MediatorFramework.Sample.Tests
NEW=$S/Core/Ark.MediatorFramework.Sample.Core.Tests
mkdir -p $NEW/Hooks $NEW/Steps
for f in Features Auth Drivers Fakes Init; do git mv $OLD/$f $NEW/$f; done
for f in Hooks/HooksOrder.cs Hooks/DatabaseHooks.cs Hooks/SampleTestContext.cs Hooks/ApplicationTestContext.cs \
         Steps/BookSteps.cs Steps/BookPrintingProcessSteps.cs \
         ApplicationTestContextTests.cs ApplicationCompositionTests.cs ConcurrencyRoundtripTests.cs \
         AsyncEnumerableStreamingTests.cs BookStreamingAndEditionTests.cs TestAssemblyConfiguration.cs \
         NativeOutboxIntegrationTests.cs; do
  git mv $OLD/$f $NEW/$f
done
cp $OLD/GlobalUsings.cs $NEW/GlobalUsings.cs
git rm $OLD/Hooks/ProcessWideApplicationTestFixture.cs $OLD/ProcessWideApplicationFixtureTests.cs \
       $OLD/Steps/RebusSteps.cs $OLD/Hooks/RebusScenarioContext.cs $OLD/RebusRetryTests.cs
grep -rl 'Ark.MediatorFramework.Sample.Tests' $NEW | xargs sed -i 's/Ark\.MediatorFramework\.Sample\.Tests/Ark.MediatorFramework.Sample.Core.Tests/g'
```

`RebusRetryTests.cs` is deleted here and re-created against the WebRebus host
in Task 7.

`NativeOutboxIntegrationTests` moves because its two SQL tests exercise the
Application's outbox context through `DatabaseHooks`, which moves too. Its third
test, `DedicatedHostResolvesExactlyOneReservedProcessor`, uses the old
`OutboxProcessorComposition` host: cut it out of the moved file and paste it,
unchanged, into a new `$OLD/OutboxProcessorCompositionTests.cs` (same usings it
needs, no `DatabaseHooks` dependency), so the old project still compiles and
the test still runs. Task 6 moves that file to `Web.Tests`. In `GlobalUsings.cs` of the new project remove any `Rebus`,
`WebInterface`, `AzureFunctions`, or `RebusProcessor` usings.

- [ ] **Step 3: Write `ParticipantProcess`**

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid.SimpleInjector;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>One messaging participant composed as its own process: container, service provider, hosted services.</summary>
public sealed class ParticipantProcess : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IReadOnlyList<IHostedService> _hosted;

    private ParticipantProcess(Container container, ServiceProvider services)
    {
        Container = container;
        _services = services;
        _hosted = services.GetServices<IHostedService>().ToArray();
    }

    /// <summary>Gets the participant's application container.</summary>
    public Container Container { get; }

    /// <summary>Gets the participant's Microsoft DI provider.</summary>
    public IServiceProvider Services => _services;

    /// <summary>Composes and starts one participant.</summary>
    /// <param name="container">The application container, already populated by <c>ApplicationComposition</c>.</param>
    /// <param name="configureMessaging">Registers the participant with <c>ConfigureArkMessaging</c>.</param>
    /// <param name="bridgeBus">
    /// <see langword="true"/> to expose the participant bus to the container; <see langword="false"/> for the
    /// dedicated outbox processor, which registers no bus.
    /// </param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The started participant.</returns>
    public static async Task<ParticipantProcess> StartAsync(
        Container container,
        Action<IServiceCollection> configureMessaging,
        bool bridgeBus = true,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configureMessaging);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddArkSolidProcessors(container);
        configureMessaging(services);
        var provider = services.BuildServiceProvider(validateScopes: true);
        if (bridgeBus)
        {
            container.RegisterSingleton<Ark.Tools.MediatorFramework.IBus>(
                () => provider.GetRequiredService<Ark.Tools.MediatorFramework.IBus>());
            container.RegisterSingleton<Ark.Tools.MediatorFramework.IBusOutboxEnlistment>(
                () => provider.GetRequiredService<Ark.Tools.MediatorFramework.IBusOutboxEnlistment>());
        }
        container.Verify();
        var process = new ParticipantProcess(container, provider);
        foreach (var hosted in process._hosted)
            await hosted.StartAsync(ctk).ConfigureAwait(false);
        return process;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var hosted in _hosted.Reverse())
            await hosted.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _services.DisposeAsync().ConfigureAwait(false);
        await Container.DisposeAsync().ConfigureAwait(false);
    }
}
```

If `ServiceProvider` registers `IBusOutboxEnlistment` under a different service
type, resolve `IBus` and cast (`MessagingBus` implements both); keep the
container registrations lazy.

- [ ] **Step 4: Write `InMemoryMessagingHarness`**

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.Tools.MediatorFramework.Messaging;

using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>Shared in-memory transport, DataBus, topology, and JSON options for the four participants.</summary>
public static class InMemoryMessagingHarness
{
    /// <summary>Gets the participant queues in the sample network.</summary>
    public static IReadOnlyList<string> Queues { get; } =
    [
        SampleMessagingParticipant.Identity,
        SampleMessagingNotificationParticipant.Identity,
        SampleMessagingAuditParticipant.Identity,
    ];

    /// <summary>Creates queues, the completed-print topic, and both subscriptions.</summary>
    /// <param name="transport">The shared in-memory transport.</param>
    /// <param name="ctk">The cancellation token.</param>
    public static async Task EnsureTopologyAsync(InMemoryMessagingTransport transport, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var maximumDeliveryCount = new SampleMessagingRetryPolicy().MaximumDeliveryCount * 2;
        foreach (var queue in Queues)
            await transport.EnsureQueueAsync(queue, maximumDeliveryCount, queue, ctk).ConfigureAwait(false);
        var topic = SampleMessagingNetwork.Registry.GetDestination<BookPrintCompleted>();
        await transport.EnsureTopicAsync(topic, SampleMessagingParticipant.Identity, ctk).ConfigureAwait(false);
        foreach (var subscriber in new[] { SampleMessagingNotificationParticipant.Identity, SampleMessagingAuditParticipant.Identity })
        {
            await transport.EnsureSubscriptionAsync(
                new MessagingSubscriptionResource(topic, subscriber, subscriber, maximumDeliveryCount, subscriber),
                ctk).ConfigureAwait(false);
        }
    }

    /// <summary>Configures the messaging JSON codec with the application source-generated context.</summary>
    /// <param name="services">The participant service collection.</param>
    public static void ConfigureJson(IServiceCollection services)
    {
        services.Configure<JsonSerializerOptions>(static options =>
        {
            options.RespectNullableAnnotations = true;
            options.RespectRequiredConstructorParameters = true;
            options.TypeInfoResolver = ApplicationJsonSerializerContext.Default;
        });
    }
}
```

Before Task 5, the topic publisher is still `SampleMessagingPublisherParticipant`;
use `SampleMessagingPublisherParticipant.Identity` in `EnsureTopicAsync` here
and switch to `SampleMessagingParticipant.Identity` in Task 5.

- [ ] **Step 5: Rewrite `ApplicationTestContext` composition**

Replace the Rebus block of the constructor (from `var rebusRequirements` to
`SetAuthenticatedUser();`) and the `Network`, `StartOutboundBus`, `SendAsync`
members. The context now owns four participant processes sharing one data
context factory, one principal provider, one transport, and one DataBus:

```csharp
    private readonly InMemoryMessagingTransport _transport = new();
    private readonly InMemoryMessagingDataBus _dataBus = new();
    private readonly List<ParticipantProcess> _processes = [];
    private ParticipantProcess? _api;

    /// <summary>Gets the shared in-memory transport.</summary>
    public InMemoryMessagingTransport Transport => _transport;

    /// <summary>Gets the notifications recorded by the notification subscriber.</summary>
    public RecordingBookPrintSink Notifications { get; } = new();

    /// <summary>Gets the audit records written by the audit subscriber.</summary>
    public RecordingBookPrintSink Audits { get; } = new();

    /// <summary>Starts the four participants. Called once per scenario before the first dispatch.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task StartAsync(CancellationToken ctk = default)
    {
        if (_api is not null)
            return;
        await InMemoryMessagingHarness.EnsureTopologyAsync(_transport, ctk).ConfigureAwait(false);
        _api = await _startAsync(_container, _principalProvider, static (b, t, d) => b.Producer<SampleMessagingPublisherParticipant>(p => p
            .UseTransport(t).UseDataBus(d).UseOutgoingPipeline(typeof(UserContextOutgoingStep)).UseOutbox()), ctk).ConfigureAwait(false);
        _processes.Add(_api);
        var workerPrincipal = new MessagePrincipalProvider();
        _processes.Add(await _startAsync(_newContainer(workerPrincipal), workerPrincipal, static (b, t, d) => b.Receiver<SampleMessagingParticipant>(r => r
            .UseTransport(t).UseDataBus(d)
            .UseIncomingPipeline(typeof(UserContextIncomingStep)).UseOutgoingPipeline(typeof(UserContextOutgoingStep))
            .UseOutbox()), ctk).ConfigureAwait(false));
        var notificationPrincipal = new MessagePrincipalProvider();
        var notification = _newContainer(notificationPrincipal);
        ApplicationComposition.RegisterNotificationSubscriber(notification, Notifications);
        _processes.Add(await _startAsync(notification, notificationPrincipal, static (b, t, d) => b.Receiver<SampleMessagingNotificationParticipant>(r => r
            .UseTransport(t).UseDataBus(d).UseIncomingPipeline(typeof(UserContextIncomingStep))), ctk).ConfigureAwait(false));
        var auditPrincipal = new MessagePrincipalProvider();
        var audit = _newContainer(auditPrincipal);
        ApplicationComposition.RegisterAuditSubscriber(audit, Audits);
        _processes.Add(await _startAsync(audit, auditPrincipal, static (b, t, d) => b.Receiver<SampleMessagingAuditParticipant>(r => r
            .UseTransport(t).UseDataBus(d).UseIncomingPipeline(typeof(UserContextIncomingStep))), ctk).ConfigureAwait(false));
        _processes.Add(await _startOutboxProcessorAsync(ctk).ConfigureAwait(false));
    }
```

Principal flow is per participant, as in production processes: the api
container keeps the scenario-controlled `_principalProvider`, and its producer
copies that principal into message headers with `UserContextOutgoingStep`. Each
receiver gets its own `MessagePrincipalProvider` (test-local copy of the
`AsyncLocal` provider defined in Task 6, file `Hooks/MessagePrincipalProvider.cs`),
fed by `UserContextIncomingStep` and forwarded by its own
`UserContextOutgoingStep` (the worker publishes). No container shares the api
provider, so a broken header round trip fails the authorization scenarios.

Supporting members (same class):

```csharp
    private Container _newContainer(IContextProvider<ClaimsPrincipal> principal)
    {
        var container = new Container { Options = { DefaultScopedLifestyle = new AsyncScopedLifestyle() } };
        ApplicationComposition.Register(container, _applicationOptions);
        container.RegisterInstance(principal);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        return container;
    }

    private async Task<ParticipantProcess> _startAsync(
        Container container,
        IContextProvider<ClaimsPrincipal> principal,
        Action<MessagingCompositionBuilder<SampleMessagingNetwork>, IMessagingTransport, IMessagingDataBus> select,
        CancellationToken ctk)
    {
        return await ParticipantProcess.StartAsync(
            container,
            services =>
            {
                InMemoryMessagingHarness.ConfigureJson(services);
                // Register step instances before ConfigureArkMessaging so a framework TryAdd keeps them.
                services.AddSingleton(new UserContextOutgoingStep(() => principal.Current));
                if (principal is MessagePrincipalProvider receiverPrincipal)
                    services.AddSingleton(new UserContextIncomingStep(receiverPrincipal.Set));
                services.AddSingleton(_testProcessingOptions);
                services.ConfigureArkMessaging<SampleMessagingNetwork>(b => select(b, _transport, _dataBus));
            },
            ctk: ctk).ConfigureAwait(false);
    }

    private async Task<ParticipantProcess> _startOutboxProcessorAsync(CancellationToken ctk)
    {
        var container = new Container { Options = { DefaultScopedLifestyle = new AsyncScopedLifestyle() } };
        return await ParticipantProcess.StartAsync(
            container,
            services =>
            {
                services.AddSingleton<IMessagingTransport>(_transport);
                services.AddArkMessagingOutboxProcessor(
                    _applicationOptions.DataContextFactory ?? _sqlOutboxFactory(),
                    batchSize: 10);
            },
            bridgeBus: false,
            ctk: ctk).ConfigureAwait(false);
    }
```

Rules for this step:

- `_applicationOptions` is built once in the constructor:
  `new ApplicationOptions { DataContextFactory = …, SqlConnectionString = …, Clock = Clock, PrintCompletedNotificationService = _printCompletedNotificationProxy }`
  with exactly one persistence field set (in-memory: a single
  `InMemorySampleDataContextFactory` instance shared by every participant;
  SQL: `DatabaseHooks.ConnectionString`). `_sqlOutboxFactory()` returns
  `new SampleDataContextFactory(new SqlConnectionManager(), new SampleDataContextConfig(DatabaseHooks.ConnectionString))`.
- `_testProcessingOptions` is a `MessagingProcessingOptions` with
  `MinPollInterval = 2 ms`, `MaxPollInterval = 200 ms` (same as the deleted
  `MessagingSourceTestExtensions._pollOptions`) so scenarios stay fast.
- `ConfigureArkMessaging<TNetwork>(Action<MessagingCompositionBuilder<TNetwork>>)`
  is the generic overload at `FluentMessagingComposition.cs:22`; if its
  parameter shape differs, use the
  `(MessagingNetworkOptions, IMessagingContractRegistry, Action<…>)` overload
  with `SampleMessagingNetwork.CreateOptions()` and `SampleMessagingNetwork.Registry`,
  as `SampleStartup` does today.
- `DispatchRequestAsync`/`DispatchQueryAsync`/`DispatchCommandAsync` call
  `await StartAsync(ctk)` first, then dispatch through `_container` exactly as
  today.
- `DisposeAsync` disposes `_processes` in reverse order (the api process owns
  `_container`), then detaches the external-service binding.
- Delete `SendAsync`, `StartOutboundBus`, `Network`, and every `Rebus` using.
- Add `Hooks/MessagePrincipalProvider.cs`: same code as
  `MessagingPrincipalContextProvider` in Task 6 Step 2 (an `AsyncLocal<ClaimsPrincipal?>`
  with `Current` and `Set`), in the `Core.Tests.Hooks` namespace. The test
  project cannot reference a host, so it keeps its own copy.
- Add `RecordingBookPrintSink` in `Fakes/`: implements both
  `IBookPrintNotificationSink` and `IBookPrintAuditSink`, stores book IDs in a
  `ConcurrentQueue<Guid>`, and exposes `IReadOnlyCollection<Guid> BookIds`.

- [ ] **Step 6: Background messaging context and steps**

`Hooks/BackgroundMessagingContext.cs` replaces `RebusScenarioContext`. It keeps
the same idle algorithm (5 consecutive idle samples, 50 ms period, 5 s
timeout) with this work count:

```csharp
    private async Task<(int Pending, int DeadLetters)> _getWorkAsync(CancellationToken ctk)
    {
        var application = _sampleContext.Application;
        var pending = await application.GetOutboxCountAsync(ctk).ConfigureAwait(false);
        var deadLetters = 0;
        foreach (var queue in InMemoryMessagingHarness.Queues)
        {
            pending += application.Transport.GetPendingCount(queue);
            deadLetters += application.Transport.GetDeadLetters(queue).Count;
        }
        return (pending, deadLetters);
    }
```

Keep the two `[When("I wait for the background bus to be idle and the outbox to be empty…")]`
bindings with the same text. `ErrorQueueCount` sums `GetDeadLetters(queue).Count`
over `Queues`.

`Steps/BackgroundMessagingSteps.cs` keeps the step texts from `RebusSteps`:

- `[When("I dispatch a book review for the current book through the background bus with")]`
  sends through the api process bus:
  `await application.SendAsync(request)` → implement as
  `await _api.Services.GetRequiredService<IBus>().Send(request, cancellationToken: ctk)`
  exposed from `ApplicationTestContext.SendAsync<T>(T message, CancellationToken ctk)`.
- `[Then("the error queue contains the failed message")]` waits with
  `allowErrors: true` and asserts `ErrorQueueCount > 0`.
- Drop the `failing background message` step (unused by any feature).

- [ ] **Step 7: Run the application scenarios**

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests
```

Expected: every scenario of `Books.feature` and every moved MSTest passes.
Then with SQL: `docker compose -f $S/docker-compose.yml up -d db` and the
same command without `ARK_SAMPLE_INMEMORY_TESTS`. Expected: all pass.

If `Reject an unauthorized book review through the background bus` does not
reach a dead letter, inspect `SampleMessagingRetryPolicy`
(`SecondLevelRetriesEnabled = true`) and the generated
`SampleMessagingParticipant.DispatchFailedAsync`: an unhandled
`MessagingFailed<CreateBookReviewRequest.V1>` must dead-letter. Fix the test
harness, not the policy. The scenario must fail for the authorization reason:
assert the dead letter's reason/description is not `UnknownContractName`.

- [ ] **Step 8: Old project still builds and passes**

The old `S/test/Ark.MediatorFramework.Sample.Tests` keeps the host-boundary
tests only. Remove its now-unused `Reqnroll*` and `Microsoft.SqlServer.DacFx`
references if nothing else uses them, and drop `Ark.Tools.Reqnroll` and the
`.sqlproj` reference only if no remaining file needs them.

```bash
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests
dotnet build Ark.Tools.slnx --configuration Debug
```

Expected: build green, remaining old tests pass.

- [ ] **Step 9: Commit**

```bash
git add -A samples/Ark.MediatorFramework.Sample Ark.Tools.slnx
git commit -m "test(samples): run application scenarios on in-memory messaging" -m "Assisted-by: Claude"
```

---

### Task 4b: Idempotent book-review creation

`CreateBookReviewRequest.V1` travels as an at-least-once message on every
variant (Task 4). `CreateBookReviewHandler` generates a new `Guid` and inserts
on every call, so a settlement failure after commit creates a duplicate review
on redelivery. The sender now supplies the review identifier, and the handler
treats a repeat as a no-op that returns the stored review. Branch:
`feature/mf-sample-04b-review-idempotency`, based on Task 4's branch; the
Task 5 branch is based on this one.

**Files:**
- Modify: `C/…Core.API/BookReviewContracts.cs` (`V1.ReviewId`)
- Modify: `C/…Core.Application/Handlers/Book/BookReviewHandlers.cs`
- Modify: `C/…Core.Application/DAL/SampleDataContext.cs` (interface + SQL `ReadBookReviewAsync`)
- Modify: `C/…Core.Application/DAL/InMemorySampleDataContextFactory.cs` (`ReadBookReviewAsync`)
- Create: `C/…Core.Application/Exceptions/BookReviewIdConflictViolation.cs`
- Modify: `C/…Core.Tests/Features/Books.feature`, `C/…Core.Tests/Steps/` (review steps and `BackgroundMessagingSteps`), `C/…Core.Tests/Drivers/BookDriver.cs`
- Modify: `C/…Core.API/ArkApiSurface.txt` (accepted diff: `V1.ReviewId`), any OpenAPI or MCP snapshot that lists the request schema
- Modify: `samples/Ark.MediatorFramework.Sample/README.md` or the Core README section on reviews (one paragraph)

**Interfaces:**
- Consumes: Task 4 `ApplicationTestContext.SendAsync<T>`, `BackgroundMessagingSteps`, `BookDriver`.
- Produces: `CreateBookReviewRequest.V1.ReviewId : Guid?` — the client-generated review id. When set, a repeated request with the same id and the same `BookId` returns the stored review and writes nothing. When unset, the handler generates one (the plain HTTP call stays non-idempotent, as it is today). Every bus sender in every variant sets it.
- Produces: `ISampleDataContext.ReadBookReviewAsync(Guid id, CancellationToken ctk = default) : Task<BookReview?>`.
- Produces: `BookReviewIdConflictViolation(Guid reviewId) : BusinessRuleViolation` — the id already belongs to a review of another book.

- [ ] **Step 1: Write the failing scenarios**

Append to `Rule: Book reviews demonstrate child-resource behavior`:

```gherkin
        Scenario: Repeating a book review with the same identifier creates it once
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I create a book review with
                | ReviewId                             | Rating | Text            |
                | 5d1f3c1e-8a59-4f39-9d55-0c6a0f7b2a11 | 5      | Excellent book! |
            And I create a book review with
                | ReviewId                             | Rating | Text            |
                | 5d1f3c1e-8a59-4f39-9d55-0c6a0f7b2a11 | 5      | Excellent book! |
            Then the book review was created
            And the last two book reviews have the same identifier
            When I list book reviews with
                | Skip | Limit |
                | 0    | 10    |
            Then the book review list has 1 results
            And the audit log has 1 entries for the book review

        Scenario: Redelivering a background book review creates it once
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user
            When I dispatch the same book review for the current book through the background bus twice with
                | Rating | Text            |
                | 5      | Excellent book! |
            And I wait for the background bus to be idle and the outbox to be empty
            When I list book reviews with
                | Skip | Limit |
                | 0    | 10    |
            Then the book review list has 1 results
            And the audit log has 1 entries for the book review

        Scenario: Reject a review identifier that belongs to another book
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            And I create a book review with
                | ReviewId                             | Rating | Text            |
                | 7c2e4b10-3d6a-4c8e-b1f2-9a0d5e6f7a22 | 5      | Excellent book! |
            And I create a book with
                | Title       | Author | Genre   |
                | Neuromancer | Gibson | Fiction |
            When I create a book review with
                | ReviewId                             | Rating | Text   |
                | 7c2e4b10-3d6a-4c8e-b1f2-9a0d5e6f7a22 | 4      | Second |
            Then the book request fails with a business rule violation
```

Reuse existing step texts where they exist (book creation, review list,
failure assertions; the business-rule failure step already exists for print
processes — use its exact text). New bindings:
- `the last two book reviews have the same identifier` — `BookDriver` keeps the
  previous and current `BookReview`.
- `the audit log has {int} entries for the book review` — counts audit entries
  with `EntityType = "BookReview"` and `Identifier` = the current review id,
  through the existing audit accessor of `ApplicationTestContext`.
- `I dispatch the same book review … twice with` — builds one V1 with
  `ReviewId = Guid.NewGuid()` and sends it twice through `SendAsync`. Two
  sends of one payload are what a redelivery looks like to the handler.

The existing `I dispatch a book review … through the background bus with`
step also sets `ReviewId = Guid.NewGuid()`: every bus sender supplies the id.

- [ ] **Step 2: Run to verify they fail**

Run: `ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests --filter "DisplayName~review"`
Expected: build error (`ReviewId` not found), then after Step 3's contract
change only, the three scenarios fail on counts / missing violation.

- [ ] **Step 3: Implement**

Contract (`BookReviewContracts.cs`, inside `V1`):

```csharp
        /// <summary>
        /// Gets the client-generated review identifier. Repeating a request with the same identifier for the
        /// same book returns the stored review and writes nothing; senders over a message bus must set it so
        /// that redelivery is idempotent. When omitted, the server generates a new identifier.
        /// </summary>
        public Guid? ReviewId { get; init; }
```

Data context: `ReadBookReviewAsync(Guid id, CancellationToken ctk = default)`
returning `BookReview?`. SQL: `SELECT [Id], [BookId], [UserId], [Rating], [Text], [CreatedAt] FROM [dbo].[BookReview] WHERE [Id] = @Id;`
in the current transaction. In-memory: `_bookReviews.TryGetValue`.

Handler, after the book existence check and before building the review:

```csharp
        var reviewId = request.ReviewId ?? Guid.NewGuid();
        var existing = await context.ReadBookReviewAsync(reviewId, ctk).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.BookId == request.BookId
                ? existing
                : throw new BusinessRuleViolationException(new BookReviewIdConflictViolation(reviewId));
        }
```

and use `reviewId` as `BookReview.Id`. No schema change: `PK_BookReview`
already rejects a concurrent duplicate insert; that delivery fails, is
retried, and then takes the early-return branch.

`BookReviewIdConflictViolation` follows `BookPrintingProcessAlreadyRunningViolation`:
title `"The review identifier is already in use."`, `Detail` naming the id,
property `ReviewId`.

Validator: `RuleFor(r => r.ReviewId).NotEqual(Guid.Empty).When(r => r.ReviewId.HasValue);`

Accept the `ArkApiSurface.txt` diff (`ReviewId` only) per Global Constraints,
and any OpenAPI/MCP schema snapshot that now lists `reviewId`.

- [ ] **Step 4: Run tests**

Run the Step 2 command; expected: all review scenarios pass. Then the whole
in-memory profile: `ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests`;
expected: no regressions. Build the old projects still in the solution
(`dotnet build Ark.Tools.slnx`); expected: success. SQL runs in CI.

- [ ] **Step 5: Docs and commit**

One paragraph in the sample README where reviews are described: bus senders
set `ReviewId`; a repeat is a no-op; the HTTP call without it is not
idempotent. No CHANGELOG entry (sample-only).

```bash
git add -A samples/Ark.MediatorFramework.Sample
git commit -m "feat(samples): make book review creation idempotent" -m "Assisted-by: Claude"
```

---

### Task 5: The print worker publishes `BookPrintCompleted`

**Files:**
- Modify: `C/…Core.Application/Messages/MessagingDeclarations.cs`
- Modify: `C/…Core.Application/Handlers/Book/ProcessBookPrintProcessHandler.cs`
- Modify: `C/…Core.Tests/Features/Books.feature`, `C/…Core.Tests/Steps/BookPrintingProcessSteps.cs`
- Modify: `C/…Core.Tests/Hooks/InMemoryMessagingHarness.cs`, `C/…Core.Tests/Hooks/ApplicationTestContext.cs` (participant rename)
- Modify: every old host that names `SampleMessagingPublisherParticipant`
- Modify: `C/…Core.Application/ArkApiSurface.txt`

**Interfaces:**
- Produces: `SampleMessagingApiParticipant` (renamed from `SampleMessagingPublisherParticipant`, declares nothing it publishes); `SampleMessagingParticipant` declares `Publishes = [typeof(BookPrintCompleted)]`.

- [ ] **Step 1: Write the failing scenario**

Append to `Rule: Book printing runs asynchronously` in `Books.feature`:

```gherkin
        Scenario: Notify and audit a completed book print
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            And I wait for the background bus to be idle and the outbox to be empty
            Then the completed print of the current book was notified and audited
```

Add a redelivery scenario to pin the idempotency boundary (spec: *Messaging
topology*):

```gherkin
        Scenario: Publish a completed book print once when its message is redelivered
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            And I wait for the background bus to be idle and the outbox to be empty
            And the current book print process message is delivered again
            And I wait for the background bus to be idle and the outbox to be empty
            Then the completed print of the current book was notified and audited
```

Add the bindings in `BookPrintingProcessSteps.cs`:

```csharp
    /// <summary>Simulates at-least-once redelivery of the worker message.</summary>
    [When("the current book print process message is delivered again")]
    public async Task CurrentProcessMessageIsDeliveredAgain()
    {
        var process = Current ?? throw new InvalidOperationException("No current book print process.");
        await _sampleContext.Application.SendAsync(new ProcessBookPrintProcessRequest { Id = process.Id })
            .ConfigureAwait(false);
    }
```

`Current` is the class's existing `BookPrintProcessResponse?` property.
`ContainSingle` in the next binding is what makes the redelivery scenario
fail if the worker publishes twice.

```csharp
    /// <summary>Asserts that both subscribers received the completed-print event.</summary>
    [Then("the completed print of the current book was notified and audited")]
    public void CompletedPrintWasNotifiedAndAudited()
    {
        var bookId = _books.Current.Id;
        _sampleContext.Application.Notifications.BookIds.Should().ContainSingle().Which.Should().Be(bookId);
        _sampleContext.Application.Audits.BookIds.Should().ContainSingle().Which.Should().Be(bookId);
    }
```

`_books` (`BookDriver`) and `_sampleContext` (`SampleTestContext`) are the
class's existing constructor-injected fields.

- [ ] **Step 2: Run to verify it fails**

Run: `ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests --filter "DisplayName~Notify and audit"`
Expected: FAIL, `Notifications.BookIds` is empty.

- [ ] **Step 3: Move the event to the worker**

`MessagingDeclarations.cs`:

```csharp
/// <summary>Declares the API participant: sends background work, publishes nothing.</summary>
[MessagingParticipant(
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json)]
public sealed partial class SampleMessagingApiParticipant;

/// <summary>Declares the print worker: processes background work and publishes completed prints.</summary>
[MessagingParticipant(
    Identity = "ark-mediator-sample",
    Processes = new[] { typeof(ProcessBookPrintProcessRequest), typeof(CreateBookReviewRequest.V1) },
    Publishes = new[] { typeof(BookPrintCompleted) },
    Serializers = new[] { SerializationProtocol.Json },
    DefaultSerializer = SerializationProtocol.Json,
    Retry = typeof(SampleMessagingRetryPolicy))]
public sealed partial class SampleMessagingParticipant;
```

Update `Members` of `SampleMessagingNetwork` to `typeof(SampleMessagingApiParticipant)`.
Rename every other reference:

```bash
grep -rl SampleMessagingPublisherParticipant $S | grep -v '/obj/\|/bin/' \
  | xargs sed -i 's/SampleMessagingPublisherParticipant/SampleMessagingApiParticipant/g'
```

In `InMemoryMessagingHarness.EnsureTopologyAsync` the topic owner is
`SampleMessagingParticipant.Identity`.

- [ ] **Step 4: Publish inside the completing transaction**

In `ProcessBookPrintProcessHandler`: add `IBus bus` to the constructor (store
in `_bus`), and replace `_persistAsync` with:

```csharp
    private async Task<BookPrintProcessResponse> _persistAsync(
        BookPrintProcessResponse process,
        CancellationToken ctk)
    {
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        if (!await context.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
        {
            var current = await context.ReadBookPrintProcessAsync(process.Id, ctk: ctk).ConfigureAwait(false);
            if (current is null)
                throw new EntityNotFoundException($"Book print process '{process.Id}' was not found.");

            await context.CommitAsync(ctk).ConfigureAwait(false);
            return current;
        }
        await context.WriteAuditAsync(_createAudit(process.Id), ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
        {
            var enlistment = _bus as IBusOutboxEnlistment
                ?? throw new InvalidOperationException("The configured messaging bus does not support outbox enlistment.");
            using var scope = enlistment.Enlist(context.OutboxContext);
            await _bus.Publish(new BookPrintCompleted { BookId = process.BookId }, cancellationToken: ctk).ConfigureAwait(false);
            await scope.CompleteAsync(ctk).ConfigureAwait(false);
        }
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }
```

The inline `IPrintCompletedNotificationService.NotifyAsync` call stays: the
`Surface a failed external print-completion notification` scenario depends on it.

- [ ] **Step 5: Run the scenarios**

Run: `ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests`
Expected: all pass, including the new scenario.

- [ ] **Step 6: Accept the topology diff**

Build with `-p:EmitCompilerGeneratedFiles=true`; the Application
`ArkApiSurface.txt` diff must show only: the participant rename, the `publishes`
move to `SampleMessagingParticipant`, the network member rename, and the event
topic owner change. Accept it.

- [ ] **Step 7: Old hosts**

Build `Ark.Tools.slnx`; fix any remaining compile errors in old hosts caused by
the rename. Run `ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests`.
Expected: green. `MessagingBusSampleTests.WebInterfaceCompositionIsPublisherOnly`
may need its subscriber assertion updated to the new topic owner.

- [ ] **Step 8: Commit**

```bash
git add -A samples/Ark.MediatorFramework.Sample
git commit -m "feat(samples): publish completed book prints from the print worker" -m "Assisted-by: Claude"
```

---

### Task 5b: Resource management for fluent native hosts

A native participant that publishes needs its topics provisioned under the
default `CreateIfMissing` lifecycle. `ConfigureArkMessaging` obtains the
`IMessagingTransportManagement` seam only as `transport as IMessagingTransportManagement`
(`MessagingServiceCollectionExtensions._addArkMessagingParticipant`, 4-argument
overload). `ServiceBusMessagingTransport` does not implement it; Service Bus
management lives in the separate `ServiceBusTransportManagement`. A Service Bus
publisher composed with `ConfigureArkMessaging` therefore fails with
"does not provide resource lifecycle management". Azure Functions composition
already passes `ServiceBusTransportManagement` explicitly; the fluent builder
needs the same seam.

The seam alone is not enough for receivers. The 4-argument overload builds a
manifest only when `PublishedTopics.Count > 0`, with `identityQueue: null` and
no subscriptions, because `MessagingParticipantDescriptor` carries no
subscription data. A fluent Service Bus receiver therefore starts against a
missing identity queue and missing forwarding subscriptions. This task also
emits the subscribed topics into the generated descriptor and builds the full
manifest — identity queue, owned and subscribed topics, forwarding
subscriptions — the same resources the Azure Functions generator emits
(`MessagingFunctionsGenerator`, `MessagingResourceManifest` block). Branch: `feature/mf-sample-05b-resource-management`, based
on Task 5's branch; the Task 6 branch is based on this one.

**Files:**
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/FluentMessagingComposition.cs` (`MessagingModeBuilder`: field + `UseResourceManagement`; `_registerCommon` passes it)
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/MessagingServiceCollectionExtensions.cs` (4-argument `_addArkMessagingParticipant` overload gains `IMessagingTransportManagement? management = null`, used as `management ?? transport as IMessagingTransportManagement`, and builds the full manifest)
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/MessagingParticipantDescriptor.cs` (optional `subscribedTopics` ctor parameter, `SubscribedTopics` property)
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Generators/MessagingNetworkGenerator.cs` (`CreateDescriptor` emits `subscribedTopics`)
- Modify: generator snapshot/approval files and `ArkApiSurface.txt` baselines that the change moves (regenerate with the repo's tooling, never by hand)
- Test: `tests/Ark.Tools.MediatorFramework.Tests/FluentMessagingResourceManagementTests.cs`
- Modify: `CHANGELOG.md` (`## [Unreleased]` → `### Added`)
- Modify: `docs/mediator-framework/host-setup-and-composition.md` (one sentence plus snippet under *Fluent native messaging composition*)

**Interfaces:**
- Produces: `public MessagingModeBuilder<TNetwork, TParticipant> MessagingModeBuilder<TNetwork, TParticipant>.UseResourceManagement(IMessagingTransportManagement management)` — throws `InvalidOperationException("A resource management seam is already selected.")` on a second call, like the other `Use*` selectors.
- Produces: `MessagingParticipantDescriptor.SubscribedTopics : IReadOnlyList<MessagingTopicResource>` — one entry per subscribed event: topic `<publisherIdentity>-<contractName>`, owner = publisher identity (the same name `PublishedTopics` gives the publisher).
- Produces: `MessagingParticipantDescriptor.KnownNetworkTopics : IReadOnlyList<string>` — every topic of the network (each member's published contracts), so reconciliation still lists a topic this participant stopped subscribing to and deletes its stale subscription, as the Functions generator's `KnownTopics` does.
- Produces: under `CreateIfMissing`, a fluent Producer or Receiver provisions: identity queue (receivers only), published and subscribed topics, one forwarding subscription per subscribed topic named and forwarded to the participant identity. Subscription names match what Task 4's `InMemoryMessagingHarness.EnsureTopologyAsync` creates, so the harness stays valid.

- [ ] **Step 1: Write the failing tests**

Use a test transport that implements `IMessagingTransport` only (no
management), the sample-independent test network/participants that
`tests/Ark.Tools.MediatorFramework.Tests` already declares for fluent
composition tests (find one whose participant publishes an event; reuse it),
and a recording `IMessagingTransportManagement` fake (the test project already
has fakes for `MessagingResourceLifecycleTests`; reuse one if it records
`EnsureTopicAsync`).

```csharp
[TestMethod]
public void PublisherWithoutManagementSeamFailsComposition()
{
    var services = new ServiceCollection();

    var act = () => services.ConfigureArkMessaging<TestNetwork>(b => b.Producer<TestPublisher>(p => p
        .UseTransport(new NonManagingTransport())
        .UseInMemoryDataBus()));

    act.Should().Throw<InvalidOperationException>().WithMessage("*resource lifecycle management*");
}

[TestMethod]
public async Task ExplicitManagementSeamProvisionsPublishedTopics()
{
    var management = new RecordingTransportManagement();
    var services = new ServiceCollection();
    services.AddLogging();
    services.ConfigureArkMessaging<TestNetwork>(b => b.Producer<TestPublisher>(p => p
        .UseTransport(new NonManagingTransport())
        .UseInMemoryDataBus()
        .UseResourceManagement(management)));
    await using var provider = services.BuildServiceProvider();

    foreach (var hosted in provider.GetServices<IHostedService>())
        await hosted.StartAsync(default).ConfigureAwait(false);

    management.EnsuredTopics.Should().NotBeEmpty();
}

[TestMethod]
public void SecondManagementSeamIsRejected()
{
    var services = new ServiceCollection();
    var management = new RecordingTransportManagement();

    var act = () => services.ConfigureArkMessaging<TestNetwork>(b => b.Producer<TestPublisher>(p => p
        .UseTransport(new NonManagingTransport())
        .UseInMemoryDataBus()
        .UseResourceManagement(management)
        .UseResourceManagement(management)));

    act.Should().Throw<InvalidOperationException>().WithMessage("*already selected*");
}

[TestMethod]
public async Task ExplicitManagementSeamProvisionsReceiverQueueAndSubscriptions()
{
    var management = new RecordingTransportManagement();
    var services = new ServiceCollection();
    services.AddLogging();
    services.ConfigureArkMessaging<TestNetwork>(b => b.Receiver<TestSubscriber>(r => r
        .UseTransport(new NonManagingMessageSourceTransport())
        .UseInMemoryDataBus()
        .UseResourceManagement(management)));
    await using var provider = services.BuildServiceProvider();

    foreach (var hosted in provider.GetServices<IHostedService>())
        await hosted.StartAsync(default).ConfigureAwait(false);

    management.EnsuredQueues.Should().Contain(TestSubscriber.Identity);
    management.EnsuredTopics.Should().Contain(TestPublisher.Identity + "-" + "<contract name of the subscribed event>");
    management.EnsuredSubscriptions.Should().ContainSingle(s =>
        s.Name == TestSubscriber.Identity && s.ForwardToQueue == TestSubscriber.Identity);
}
```

`TestSubscriber` is a test participant that subscribes to the event
`TestPublisher` publishes; `NonManagingMessageSourceTransport` implements
`IMessagingTransport` and `IMessagingMessageSource` (a receiver requires a
message source) but not `IMessagingTransportManagement`. Replace the
`<contract name …>` placeholder with the derived name the generator gives the
event (read it from `TestPublisher`'s generated `PublishedTopics`). Stop the
hosted services in reverse order at the end of each test. Also add one
generator test (in the existing `MessagingNetworkGenerator` test class) that
asserts the emitted `CreateDescriptor` passes the subscribed topic and every
network topic as `knownNetworkTopics`, and regenerate any snapshot the change
moves. Add one reconciliation test: the recording management returns, for a
network topic the receiver does not subscribe to, an existing subscription
owned by the receiver; after start it was deleted.

Adapt `TestNetwork`/`TestPublisher`/`NonManagingTransport`/`RecordingTransportManagement`
to the names the test project already uses; create only what does not exist.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Ark.Tools.MediatorFramework.Tests --filter "FullyQualifiedName~FluentMessagingResourceManagementTests"`
Expected: build error, `UseResourceManagement` not found.

- [ ] **Step 3: Implement**

In `MessagingModeBuilder<TNetwork, TParticipant>`:

```csharp
    private IMessagingTransportManagement? _resourceManagement;

    /// <summary>Uses an explicit resource-management seam for <c>CreateIfMissing</c> provisioning.</summary>
    /// <remarks>
    /// Needed when the transport does not implement <see cref="IMessagingTransportManagement"/> itself,
    /// for example <c>ServiceBusMessagingTransport</c> with <c>ServiceBusTransportManagement</c>.
    /// </remarks>
    /// <param name="management">The resource-management seam.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseResourceManagement(
        IMessagingTransportManagement management)
    {
        ArgumentNullException.ThrowIfNull(management);
        _resourceManagement = _resourceManagement is null
            ? management
            : throw new InvalidOperationException("A resource management seam is already selected.");
        return this;
    }
```

`_registerCommon` passes `_resourceManagement` to `_addArkMessagingParticipant`;
the 4-argument overload adds the optional `management` parameter and uses
`management ?? transport as IMessagingTransportManagement`.

`MessagingParticipantDescriptor` gains last optional ctor parameters
`IEnumerable<MessagingTopicResource>? subscribedTopics = null` and
`IEnumerable<string>? knownNetworkTopics = null`, stored like
`PublishedTopics`. The generator emits `knownNetworkTopics` from every network
member's `Publishes` (`<memberIdentity>-<contractName>`, ordinal-sorted). In `MessagingNetworkGenerator`, `CreateDescriptor` emits it
after the published-topics array: for each `participant.Subscribes` contract
with exactly one publisher in the network (other cases already report a
diagnostic), `new MessagingTopicResource("<publisherIdentity>-<contractName>", "<publisherIdentity>")`.

The 4-argument overload builds the manifest from both lists:

```csharp
        var hasResources = participant.PublishedTopics.Count > 0 || participant.Receives;
        var maximumDeliveryCount = checked(participant.RetryPolicy.MaximumDeliveryCount
            * (participant.RetryPolicy.SecondLevelRetriesEnabled ? 2 : 1));
        var topics = participant.PublishedTopics.Concat(participant.SubscribedTopics).ToArray();
        var resources = participant.Network.ResourceLifecycle == MessagingResourceLifecycle.CreateIfMissing
            && hasResources
                ? new MessagingResourceManifest(
                    participant.Identity,
                    participant.Receives ? participant.Identity : null,
                    maximumDeliveryCount,
                    topics,
                    participant.SubscribedTopics.Select(topic => new MessagingSubscriptionResource(
                        topic.Name,
                        participant.Identity,
                        participant.Identity,
                        maximumDeliveryCount,
                        participant.Identity)),
                    participant.KnownNetworkTopics,
                    participant.Network.ResourceLifecycle)
                : null;
```

The delivery count matches the Functions generator (`MaximumDeliveryCount`,
doubled when second-level retries are on). Before this task a fluent receiver
with no published topics provisioned nothing; it now provisions its queue and
subscriptions, so an in-memory or Service Bus receiver needs a management seam
(the in-memory transport is its own). Callers on `External` are unchanged.

- [ ] **Step 4: Run tests**

Run the Step 2 command; expected: 4 passed. Then
`dotnet test tests/Ark.Tools.MediatorFramework.Tests --filter "FullyQualifiedName~Messaging"`;
expected: no regressions.

- [ ] **Step 5: Docs and changelog**

CHANGELOG `Added`:
`- Fluent native messaging composition accepts an explicit resource-management seam (UseResourceManagement) and provisions a receiver's queue, topics and forwarding subscriptions, so Service Bus hosts outside Azure Functions can self-provision under CreateIfMissing.`

`host-setup-and-composition.md`, after the first fluent snippet:

```csharp
.UseTransport(transport => transport.UseServiceBus(client))
.UseResourceManagement(new ServiceBusTransportManagement(administrationClient))
```

with two sentences: a transport that does not manage its own resources needs an
explicit seam when the network uses `CreateIfMissing`; with it, Producers
provision their topics and Receivers also their identity queue and forwarding
subscriptions.

- [ ] **Step 6: Commit**

```bash
git add src/mediator-framework tests/Ark.Tools.MediatorFramework.Tests \
  CHANGELOG.md docs/mediator-framework/host-setup-and-composition.md
git commit -m "feat(MediatorFramework): provision fluent messaging host resources" -m "Assisted-by: Claude"
```

---

### Task 6: `Web` variant (Minimal API + gRPC + MCP + native messaging)

Replaces the old `WebInterface`, `OutboxProcessor`, and `GrpcClient` projects.

**Files:**
- Create: `W/Ark.MediatorFramework.Sample.Core.Web.Hosting/` — `…Hosting.csproj`, `WebHosting.cs`, `MessagingPrincipalContextProvider.cs`
- Move + rename: `S/src/Ark.MediatorFramework.Sample.WebInterface` → `W/Ark.MediatorFramework.Sample.Core.Web.WebInterface` (git mv, then edits below)
- Create: `W/…Core.Web.Processor/`, `W/…Core.Web.NotificationProcessor/`, `W/…Core.Web.AuditProcessor/` — each `*.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`
- Move + rename: `S/src/Ark.MediatorFramework.Sample.OutboxProcessor` → `W/…Core.Web.OutboxProcessor`
- Move + rename: `S/test/Ark.MediatorFramework.Sample.GrpcClient` → `W/…Core.Web.GrpcClient`
- Create: `W/…Core.Web.Tests/` — moved from the old test project: `BookTransportBoundaryTests.cs`, `CompositionRootTests.cs`, `NodaTimeContractRoundtripTests.cs`, `ApplicationInsightsTests.cs`, `OutboxProcessorCompositionTests.cs`
- Create: `W/README.md`
- Modify: `S/Directory.Build.props` (`ArkExportProtoDir` → `$(MSBuildThisFileDirectory)Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface/proto`)
- Modify: both solution files

**Interfaces:**
- Consumes: Task 3 `ApplicationComposition`, `ApplicationOptions`; Task 5 participants.
- Produces:
  - `WebHosting.CreateContainer(ApplicationOptions options) : Container` — `Register` + authorization + `ScopeAuthorizationHandler`.
  - `WebHosting.AddParticipant<TParticipant>(IServiceCollection services, Container container, IMessagingTransport transport, IMessagingDataBus dataBus, IMessagingTransportManagement? resourceManagement, bool receiver)` — JSON codec options, `AddArkSolidProcessors`, user-context steps, `ConfigureArkMessaging` Producer or Receiver (both with `UseOutbox()`, plus `UseResourceManagement` when a manager is given).
  - `WebHosting.CreateResourceManagement(IConfiguration configuration) : IMessagingTransportManagement`.
- Consumes: Task 5b `MessagingModeBuilder.UseResourceManagement(IMessagingTransportManagement)`.
  - `WebHosting.BridgeBus(Container container, Func<IServiceProvider> services)` — lazy `IBus`/`IBusOutboxEnlistment` registrations resolved from the built provider.
  - `MessagingPrincipalContextProvider : IContextProvider<ClaimsPrincipal>` with `void Set(ClaimsPrincipal principal)` backed by `AsyncLocal`.

- [ ] **Step 1: Write the failing composition test**

`W/…Core.Web.Tests/ProcessorCompositionTests.cs`:

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

[TestClass]
public sealed class ProcessorCompositionTests
{
    [TestMethod]
    public async Task EachProcessorHostsExactlyOneReceiver()
    {
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var transport = new InMemoryMessagingTransport();
        var dataBus = new InMemoryMessagingDataBus();
        await using var container = WebHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterAuditSubscriber(container, new NoOpBookPrintAuditSink());
        var services = new ServiceCollection();
        services.AddLogging();

        WebHosting.AddParticipant<SampleMessagingAuditParticipant>(services, container, transport, dataBus, resourceManagement: null, receiver: true);
        await using var provider = services.BuildServiceProvider();
        WebHosting.BridgeBus(container, () => provider);
        container.Verify();

        provider.GetServices<IHostedService>().OfType<MessagingProcessorHost>().Should().ContainSingle();
    }
}
```

Run: `dotnet test W/Ark.MediatorFramework.Sample.Core.Web.Tests --filter "FullyQualifiedName~ProcessorCompositionTests"`
Expected: build error, `WebHosting` not found. (Create the test project first:
copy the old test `.csproj`, drop Reqnroll/DacFx/Rebus/Functions references,
reference `…Core.Web.Hosting`, `…Core.Web.WebInterface`, `…Core.Web.GrpcClient`,
`…Core.API`, `…Core.Application`.)

- [ ] **Step 2: `Core.Web.Hosting`**

`…Hosting.csproj`: `Microsoft.NET.Sdk`, `net10.0`; `FrameworkReference Microsoft.AspNetCore.App`;
packages `Ark.Tools.MediatorFramework.Messaging`, `Ark.Tools.MediatorFramework.Messaging.Azure`,
`Ark.Tools.Solid.SimpleInjector`, `Ark.Tools.Solid.Authorization`; project reference
`…Core.Application`.

`MessagingPrincipalContextProvider.cs`:

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Web.Hosting;

/// <summary>Exposes the principal restored from the current message to the application.</summary>
public sealed class MessagingPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
{
    // Instance field, not static: each participant (and each test participant in one process)
    // owns its ambient principal, so nothing leaks between providers.
    private readonly AsyncLocal<ClaimsPrincipal?> _current = new();

    /// <inheritdoc />
    public ClaimsPrincipal Current => _current.Value ?? new ClaimsPrincipal(new ClaimsIdentity());

    /// <summary>Sets the principal for the current message flow.</summary>
    /// <param name="principal">The restored principal.</param>
    public void Set(ClaimsPrincipal principal)
    {
        _current.Value = principal;
    }
}
```

`WebHosting.cs`:

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Solid.SimpleInjector;

using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.Web.Hosting;

/// <summary>Composition shared by every process of the Web variant.</summary>
public static class WebHosting
{
    /// <summary>Creates the application container for one process.</summary>
    /// <param name="options">The application options.</param>
    /// <returns>The populated, unverified container.</returns>
    public static Container CreateContainer(ApplicationOptions options)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(container, options);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        return container;
    }

    /// <summary>Registers one messaging participant and bridges its bus into the container.</summary>
    /// <typeparam name="TParticipant">The participant hosted by this process.</typeparam>
    /// <param name="services">The process service collection.</param>
    /// <param name="container">The application container.</param>
    /// <param name="transport">The messaging transport.</param>
    /// <param name="dataBus">The claim-check DataBus.</param>
    /// <param name="resourceManagement">
    /// Provisions the participant's published topics (`CreateIfMissing`). Production passes
    /// <c>ServiceBusTransportManagement</c>; tests pass <see langword="null"/> because the in-memory
    /// transport manages its own resources.
    /// </param>
    /// <param name="receiver"><see langword="true"/> to host a processor; <see langword="false"/> for a producer.</param>
    public static void AddParticipant<TParticipant>(
        IServiceCollection services,
        Container container,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        IMessagingTransportManagement? resourceManagement,
        bool receiver)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        services.Configure<JsonSerializerOptions>(static options =>
        {
            options.RespectNullableAnnotations = true;
            options.RespectRequiredConstructorParameters = true;
            options.TypeInfoResolver = ApplicationJsonSerializerContext.Default;
        });
        services.AddArkSolidProcessors(container);
        if (receiver)
        {
            var principal = new MessagingPrincipalContextProvider();
            container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(principal);
            // Steps are resolved by type from DI (MessagingPipelineInvoker); register the
            // instances before ConfigureArkMessaging so a framework TryAdd keeps them.
            services.AddSingleton(new UserContextIncomingStep(principal.Set));
            services.AddSingleton(new UserContextOutgoingStep(() => principal.Current));
            services.ConfigureArkMessaging(
                SampleMessagingNetwork.CreateOptions(),
                SampleMessagingNetwork.Registry,
                messaging => messaging.Receiver<TParticipant>(r =>
                {
                    r.UseTransport(transport)
                        .UseDataBus(dataBus)
                        .UseIncomingPipeline(typeof(UserContextIncomingStep))
                        .UseOutgoingPipeline(typeof(UserContextOutgoingStep))
                        .UseOutbox();
                    if (resourceManagement is not null)
                        r.UseResourceManagement(resourceManagement);
                }));
        }
        else
        {
            services.AddSingleton(new UserContextOutgoingStep(
                () => container.GetInstance<IContextProvider<ClaimsPrincipal>>().Current));
            services.ConfigureArkMessaging(
                SampleMessagingNetwork.CreateOptions(),
                SampleMessagingNetwork.Registry,
                messaging => messaging.Producer<TParticipant>(p =>
                {
                    p.UseTransport(transport)
                        .UseDataBus(dataBus)
                        .UseOutgoingPipeline(typeof(UserContextOutgoingStep))
                        .UseOutbox();
                    if (resourceManagement is not null)
                        p.UseResourceManagement(resourceManagement);
                }));
        }
    }

    /// <summary>Creates the Service Bus resource manager that provisions published topics.</summary>
    /// <remarks>
    /// Reads <c>ConnectionStrings:ServiceBusAdministration</c>, falling back to
    /// <c>ConnectionStrings:ServiceBus</c>. A namespace uses one connection for both; the local
    /// emulator serves administration on a different port, so development settings set both keys.
    /// </remarks>
    /// <param name="configuration">The process configuration.</param>
    /// <returns>The resource manager.</returns>
    public static IMessagingTransportManagement CreateResourceManagement(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("ServiceBusAdministration")
            ?? configuration.GetConnectionString("ServiceBus")
            ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
        return new ServiceBusTransportManagement(new ServiceBusAdministrationClient(connection));
    }

    /// <summary>Creates the claim-check DataBus shared by every Web process.</summary>
    /// <remarks>
    /// Every process of the variant must read the attachments the others write, so the store is
    /// Azure Blob Storage, never the process-local in-memory DataBus. Locally the connection points
    /// at Azurite from docker-compose.
    /// </remarks>
    /// <param name="configuration">The process configuration.</param>
    /// <returns>The shared DataBus.</returns>
    public static IMessagingDataBus CreateDataBus(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new AzureBlobMessagingDataBus(new AzureBlobDataBusOptions
        {
            ContainerName = "amf1-databus",
            Prefix = "sample/",
            MinimumAttachmentLifetime = TimeSpan.FromDays(7),
            ConnectionString = configuration.GetConnectionString("DataBus")
                ?? throw new InvalidOperationException("ConnectionStrings:DataBus is required."),
        });
    }

    /// <summary>Exposes the participant bus, which lives in Microsoft DI, to the application container.</summary>
    /// <param name="container">The application container.</param>
    /// <param name="services">Returns the built root provider; called lazily on first resolution.</param>
    public static void BridgeBus(Container container, Func<IServiceProvider> services)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(services);
        container.RegisterSingleton<IBus>(() => services().GetRequiredService<IBus>());
        container.RegisterSingleton<IBusOutboxEnlistment>(() => services().GetRequiredService<IBusOutboxEnlistment>());
    }
}
```

`BridgeBus` callers: the WebInterface calls it inside
`ArkMinimalApiHostOptions.CrossWireContainer` (`(container, sp) => WebHosting.BridgeBus(container, () => sp)`);
processors call it before `builder.Build()` with a closure over the built host
(Step 4). The container is verified only after the provider exists.
`UseOutbox()` on a receiver enables enqueue enlistment only; the worker needs
it to publish through the outbox, and it is harmless on subscribers. If
`IBusOutboxEnlistment` is not registered as its own service, resolve `IBus`
and cast (`MessagingBus` implements both).

- [ ] **Step 3: WebInterface**

```bash
git mv $S/src/Ark.MediatorFramework.Sample.WebInterface $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface
git mv $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface/Ark.MediatorFramework.Sample.WebInterface.csproj \
       $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface/Ark.MediatorFramework.Sample.Core.Web.WebInterface.csproj
```

Edits:

- Namespace → `Ark.MediatorFramework.Sample.Core.Web.WebInterface`.
- Delete `SampleComposition.cs`, `SampleBusHostedService.cs`,
  `HostUserContextProvider.cs`, `SampleApiContainerHostedService.cs` (container
  disposal moves to `Program.cs` `await using`).
- `SampleStartup` constructor: `(Container container, IMessagingTransport transport, IMessagingDataBus dataBus, IHostEnvironment environment, IConfiguration configuration)`.
  Remove `InMemNetwork`, `useSqlStore`, `connectionString`,
  `sharedDataContextFactory`, `configureFallbackPolicy` (always `true`).
- `ConfigureServices`: delete the `InMemNetwork` registration, both
  `IHostedService` registrations, `OnContainerVerified = … StartBus()`, and
  the inline `ConfigureArkMessaging` producer; call
  `WebHosting.AddParticipant<SampleMessagingApiParticipant>(services, _container, _transport, _dataBus, _resourceManagement, receiver: false)`
  (`SampleStartup` and `SampleHost.Configure` gain an `IMessagingTransportManagement? resourceManagement`
  parameter next to `dataBus`; `Program.cs` passes `WebHosting.CreateResourceManagement(builder.Configuration)`,
  tests pass `null`).
  In `CrossWireContainer` also register
  `IContextProvider<ClaimsPrincipal>` as `AspNetCoreUserContextProvider` over the
  forwarded `IHttpContextAccessor`, and call
  `WebHosting.BridgeBus(container, () => serviceProvider)`.
- `SampleHost.Configure(WebApplicationBuilder builder, Container container, IMessagingTransport transport, IMessagingDataBus dataBus)`.
- csproj: drop `Ark.Tools.MediatorFramework.Rebus`, `Ark.Tools.Rebus`, the
  `RebusProcessor` project reference; reference `…Core.Web.Hosting`.
- `Program.cs`:

```csharp
try
{
    var builder = WebApplication.CreateBuilder(args);
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
    await using var container = WebHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
    await using var transport = new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus));
    var startup = SampleHost.Configure(
        builder, container, transport, WebHosting.CreateDataBus(builder.Configuration),
        WebHosting.CreateResourceManagement(builder.Configuration));
    var app = builder.Build();
    startup.Configure(app);
    await app.RunAsync().ConfigureAwait(false);
}
```

Keep the existing `catch`/`finally` NLog blocks unchanged.
`appsettings.Development.json` holds the local SQL emulator connection under
`ConnectionStrings:Sample`, the Service Bus emulator connection under
`ConnectionStrings:ServiceBus`, and `UseDevelopmentStorage=true` (Azurite) under
`ConnectionStrings:DataBus` (all from `docker-compose.yml`), plus
`ConnectionStrings:ServiceBusAdministration` for the emulator's administration
port. Every Web process
uses the same three keys. The in-memory DataBus appears only in tests, where
all participants share one instance in one process.
If `ServiceBusMessagingTransport` is not `IAsyncDisposable`, replace
`await using` with a hosted-service-owned disposal as `OutboxProcessor/Program.cs` does today.

- [ ] **Step 4: Processors**

Create three console projects (`Microsoft.NET.Sdk`, `OutputType Exe`,
`NoWarn ARKPII013` as the old `RebusProcessor`), each referencing
`…Core.Web.Hosting` and `Ark.Tools.NLog.Configuration`. `Program.cs` of
`…Core.Web.Processor`:

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.NLog;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using NLog;

using System.Globalization;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.ConfigureNLog("Ark.MediatorFramework.Sample.Core.Web.Processor");
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
    await using var container = WebHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
    await using var transport = new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus));
    WebHosting.AddParticipant<SampleMessagingParticipant>(
        builder.Services, container, transport, WebHosting.CreateDataBus(builder.Configuration),
        WebHosting.CreateResourceManagement(builder.Configuration), receiver: true);
    IHost? built = null;
    WebHosting.BridgeBus(container, () => built?.Services
        ?? throw new InvalidOperationException("The host is not built yet."));
    using var host = builder.Build();
    built = host;
    container.Verify();
    await host.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    LogManager.GetLogger("Main").Fatal(ex, CultureInfo.InvariantCulture, "Unhandled host failure: {Message}", ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Flush(TimeSpan.FromSeconds(5));
    LogManager.Shutdown();
}
```

`ConfigureNLog` on `HostApplicationBuilder`: use the existing Ark.Tools.NLog
extension the WebInterface uses (`builder.Host.ConfigureNLog(...)`); if only
the `IHostBuilder` form exists, use `NLogConfigurer.For(name).WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration).Apply()`
as the Functions `Program.cs` does today.

`…NotificationProcessor` is identical with
`ApplicationComposition.RegisterNotificationSubscriber(container, new NoOpBookPrintNotificationSink());`
before `AddParticipant<SampleMessagingNotificationParticipant>`. `…AuditProcessor`
uses `RegisterAuditSubscriber(container, new NoOpBookPrintAuditSink())` and
`SampleMessagingAuditParticipant`.

- [ ] **Step 5: OutboxProcessor and GrpcClient**

```bash
git mv $S/src/Ark.MediatorFramework.Sample.OutboxProcessor $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.OutboxProcessor
git mv $S/test/Ark.MediatorFramework.Sample.GrpcClient $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.GrpcClient
```

Rename csproj files and namespaces as in Step 3. Rewrite
`OutboxProcessor/Program.cs` on `Host.CreateApplicationBuilder(args)` like the
other Web processes (Step 4), so configuration comes from the generic host
defaults: `appsettings.json`, `appsettings.{Environment}.json` (local values in
`appsettings.Development.json`), then environment variables. Read
`ConnectionStrings:Sample` and `ConnectionStrings:ServiceBus` from
`builder.Configuration` instead of the two `ARK_SAMPLE_*` environment
variables, register the processor with `AddArkMessagingOutboxProcessor`, and run
the host. In the GrpcClient
csproj, update `AdditionalImportDirs` to
`../Ark.MediatorFramework.Sample.Core.Web.WebInterface/proto`.

- [ ] **Step 6: Port the host-boundary tests**

```bash
OLD=$S/test/Ark.MediatorFramework.Sample.Tests
T=$S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests
for f in BookTransportBoundaryTests.cs CompositionRootTests.cs NodaTimeContractRoundtripTests.cs \
         ApplicationInsightsTests.cs OutboxProcessorCompositionTests.cs; do git mv $OLD/$f $T/$f; done
```

Rewrite each test's arrange block to the new seams:

```csharp
var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
var transport = new InMemoryMessagingTransport();
await using var container = WebHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(SampleHost).Assembly.GetName().Name,
    EnvironmentName = "IntegrationTests",
    ContentRootPath = AppContext.BaseDirectory,
});
builder.WebHost.UseTestServer();
var startup = SampleHost.Configure(builder, container, transport, new InMemoryMessagingDataBus(), resourceManagement: null);
await using var app = builder.Build();
startup.Configure(app);
await app.StartAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
```

`OutboxProcessorCompositionTests` now targets the Web variant's
`OutboxProcessor` composition. The ported `BookTransportBoundaryTests` only
inspects route metadata and invokes the gRPC service by reflection, so add the
hosted round trips the spec promises, all on the TestServer arrange block above
and the same authenticated `HttpClient` helper:

- `HttpRoundTripTests.CreateThenGetBook` — `POST /api/v1/books` with a
  `Book.V1.Create` body, assert 200 and an `id`; `GET /api/v1/books/{id}`,
  assert the same title. Take the exact routes from the generated
  `[HttpEndpoint]` metadata of `Book_CreateRequest.V1` / `Book_GetQuery.V1`.
- `GrpcClientRoundTripTests.CreateThenGetBookThroughGeneratedClient` — build a
  `GrpcChannel.ForAddress(server.BaseAddress, new GrpcChannelOptions { HttpHandler = server.CreateHandler() })`,
  use the client generated in `…Core.Web.GrpcClient` from the exported protos,
  attach the bearer as call metadata, create a book and read it back.
- `OpenApiDocumentTests.V1DocumentMatchesSnapshot` — `GET /openapi/v1.json`,
  compare (normalized line endings) with the committed
  `W/…Core.Web.Tests/Snapshots/openapi.v1.json`; on mismatch write
  `openapi.v1.received.json` next to it and fail with the accept instruction
  (copy received over the snapshot). Create the snapshot on the first run and
  commit it.
- `McpToolsListIncludesBookTools` — `POST /mcp/v1` `tools/list` with the
  integration-test bearer and assert `books.get` is present.

- [ ] **Step 7: Run**

```bash
dotnet test $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests
dotnet build Ark.Tools.slnx --configuration Debug
```

Expected: all green; `ProcessorCompositionTests` passes; proto files are
exported under the new WebInterface `proto/` folder.

- [ ] **Step 8: README and commit**

`W/README.md`: process table (5 processes + what each hosts), required
configuration keys (`ConnectionStrings:Sample`, `ConnectionStrings:ServiceBus`,
`ConnectionStrings:DataBus`, `EntraId:*`), `docker compose up -d sqlserver servicebus azurite` and one `dotnet run` per
process, and the minimum set for a scenario (WebInterface + Processor +
OutboxProcessor for the print workflow).

Update `.vscode/launch.json` program/cwd paths to the new WebInterface.

```bash
git add -A samples/Ark.MediatorFramework.Sample Ark.Tools.slnx .vscode/launch.json
git commit -m "feat(samples): add web host variant with native messaging" -m "Assisted-by: Claude"
```

---

### Task 7: `WebRebus` variant (Minimal API + Rebus) and Rebus-free Application

Replaces the old `RebusProcessor` and moves all Rebus configuration out of
Application and API.

**Files:**
- Create: `WR/…Core.WebRebus.Hosting/` — `…Hosting.csproj`, `RebusHosting.cs`
- Create: `WR/…Core.WebRebus.WebInterface/` — `…csproj`, `Program.cs`, `WebRebusStartup.cs`, `appsettings*.json`
- Move + rename: `S/src/Ark.MediatorFramework.Sample.RebusProcessor` → `WR/…Core.WebRebus.Processor`
- Create: `WR/…Core.WebRebus.NotificationProcessor/`, `WR/…Core.WebRebus.AuditProcessor/`
- Create: `WR/…Core.WebRebus.Tests/` — `RebusTopologyTests.cs`, `CompositionRootTests.cs`
- Create: `WR/README.md`
- Modify: `C/…Core.Application/Host/ApplicationComposition.cs` (delete `ConfigureRebusOutbox`, `ConfigureRebusCommon`, `RegisterOutboundRebus`)
- Modify: `C/…Core.Application/*.csproj`, `C/…Core.API/*.csproj` (drop Rebus packages)
- Modify: `C/…Core.API/BookReviewContracts.cs`, `C/…Core.Application/Messages/ProcessBookPrintProcessRequest.cs` (drop `[RebusMessage]`)
- Delete: `C/…Core.Application/Messages/DeadLetterContracts.cs`, `C/…Core.Application/Handlers/FailingRebusRequestHandler.cs`, their registration and JSON context entries
- Delete: `S/src/Ark.MediatorFramework.Sample.AzureFunctions/AzureFunctionsRebusComposition.cs` and its `Program.cs` branch and tests in `AzureFunctionsRebusTests.cs` that cover outbound Rebus

**Interfaces:**
- Produces:
  - `RebusHosting.CreateContainer(ApplicationOptions options) : Container`
  - `RebusHosting.Configure<THost>(Container container, Action<StandardConfigurer<ITransport>> transport, bool startOutboxProcessor, Action<OptionsConfigurer>? configureOptions = null, Action<RebusConfigurer>? configureTest = null) where THost : IArkRebusHost` — serializer (`ApplicationJsonSerializerContext`), `logging.NLog()`, `AutomaticallyFlowUserContext`, OpenTelemetry, generated routing/options of `THost`, outbox, `RebusMessagingBus` registered as `IBus`/`IBusOutboxEnlistment`.

- [ ] **Step 1: Write the failing topology test**

`WR/…Core.WebRebus.Tests/RebusTopologyTests.cs`, using Rebus `InMemNetwork`
and `Ark.Tools.Rebus.Tests` (same packages as the old project):

```csharp
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using AwesomeAssertions;

using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

[TestClass]
public sealed class RebusTopologyTests
{
    [TestMethod]
    public async Task CompletedPrintReachesBothRebusSubscribers()
    {
        var network = new InMemNetwork();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var notifications = new RecordingSink();
        var audits = new RecordingSink();
        await using var api = await WebRebusTestHosts.ApiAsync(network, factory).ConfigureAwait(false);
        await using var worker = await WebRebusTestHosts.WorkerAsync(network, factory).ConfigureAwait(false);
        await using var notification = await WebRebusTestHosts.NotificationAsync(network, factory, notifications).ConfigureAwait(false);
        await using var audit = await WebRebusTestHosts.AuditAsync(network, factory, audits).ConfigureAwait(false);

        var book = await api.DispatchAsync<Book_CreateRequest.V1, Book.V1.Output>(WebRebusTestHosts.NewBook())
            .ConfigureAwait(false);
        await api.DispatchAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(
            new CreateBookPrintProcessRequest.V1 { BookId = book.Id }).ConfigureAwait(false);

        await WebRebusTestHosts.WaitUntilAsync(() => notifications.Count == 1 && audits.Count == 1)
            .ConfigureAwait(false);
        notifications.BookIds.Should().ContainSingle().Which.Should().Be(book.Id);
    }

    [TestMethod]
    public async Task UnauthorizedBackgroundReviewMovesToErrorQueue()
    {
        var network = new InMemNetwork();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        await using var api = await WebRebusTestHosts.ApiAsync(network, factory).ConfigureAwait(false);
        await using var worker = await WebRebusTestHosts.WorkerAsync(network, factory).ConfigureAwait(false);
        var book = await api.DispatchAsync<Book_CreateRequest.V1, Book.V1.Output>(WebRebusTestHosts.NewBook())
            .ConfigureAwait(false);

        api.SetScopes(ApplicationScopes.BookRead, ApplicationScopes.BookWrite);
        await api.SendAsync(new CreateBookReviewRequest.V1 { ReviewId = Guid.NewGuid(), BookId = book.Id, Rating = 5, Text = "Good" })
            .ConfigureAwait(false);

        await WebRebusTestHosts.WaitUntilAsync(() => network.GetCount("error") > 0).ConfigureAwait(false);
    }
}
```

`WebRebusTestHosts` and `RebusTestProcess` (test-only, same project):

- `RebusTestProcess : IAsyncDisposable` wraps one container; `DispatchAsync<TRequest,TResponse>`
  begins an `AsyncScopedLifestyle` scope and calls the decorated
  `IRequestHandler<TRequest,TResponse>`; `SendAsync<T>` calls
  `container.GetInstance<Ark.Tools.MediatorFramework.IBus>().Send(message)`;
  `SetScopes(params string[])` sets the principal on a settable
  `IContextProvider<ClaimsPrincipal>` registered only in the api container
  (default: all `ApplicationScopes`); `DisposeAsync` disposes the container
  (which stops the bus).
- Factories are `async` (`ApiAsync`, `WorkerAsync`, `NotificationAsync`,
  `AuditAsync`) because starting a generated Rebus host does not subscribe it:
  after `container.StartBus()` each factory calls
  `await THost.SubscribeAsync(container.GetInstance<global::Rebus.Bus.IBus>())`
  (`IArkRebusHost.SubscribeAsync`). Rebus in-memory transport pub/sub needs a
  shared subscription store: pass one `InMemorySubscriberStore` to every test
  process and configure
  `cfg.Subscriptions(s => s.StoreInMemory(subscriberStore))` through an
  optional `Action<RebusConfigurer>? configureTest` argument on
  `RebusHosting.Configure`; production uses Azure Service Bus topics and needs
  no subscription storage.
- `ApiAsync(network, factory)`: `RebusHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory })`,
  settable principal provider,
  `RebusHosting.Configure<ApiRebusHost>(c, t => t.UseDrainableInMemoryTransportAsOneWayClient(network), startOutboxProcessor: false)`.
- `Worker`: `startOutboxProcessor: true` — the worker drains the shared outbox
  table, including rows the api enlisted, exactly as the old `RebusProcessor`
  did. `Notification`/`Audit`: `startOutboxProcessor: false` (they enlist
  nothing).
- `Worker`/`Notification`/`Audit`: `RebusPrincipalContextWithFallbackProvider`,
  matching `RegisterNotificationSubscriber`/`RegisterAuditSubscriber`,
  `RebusHosting.Configure<THost>(c, t => t.UseInMemoryTransport(network, <queue>), …)`
  with `timeouts.StoreInMemoryTests()` and `options.AddInProcessMessageInspector()`
  applied through an optional `Action<OptionsConfigurer>? configureOptions`
  parameter on `RebusHosting.Configure`. `<queue>` is the participant identity
  (`SampleMessagingParticipant.Identity`, …).
- Each factory calls `container.Verify(); container.StartBus();` and then
  `await THost.SubscribeAsync(...)` before returning.
- `NewBook()` returns `new Book_CreateRequest.V1(new Book.V1.Create { Title = "Dune", Author = "Herbert", Genre = Book.V1.Genre.Fiction })`.
- `WaitUntilAsync(Func<bool> condition)` polls every 50 ms and throws
  `TimeoutException` after 5 s.
- `RecordingSink` implements `IBookPrintNotificationSink` and `IBookPrintAuditSink`
  with a `ConcurrentQueue<Guid>`; exposes `Count` and `BookIds`.

Record in `WR/README.md` that the worker drains the shared outbox table for
the whole variant.

Run: `dotnet test $S/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Tests`
Expected: build error.

- [ ] **Step 2: `RebusHosting`**

Move the bodies of `ConfigureRebusOutbox` and `ConfigureRebusCommon` from
`ApplicationComposition` into `RebusHosting` unchanged except:
`AutomaticallyFlowUserContext(container)` stays; routing comes from
`THost.ConfigureRouting`; options apply `THost.ConfigureOptions`. Then:

```csharp
    /// <summary>Configures Rebus for one WebRebus process.</summary>
    /// <typeparam name="THost">The generated Rebus host bound to the process participant.</typeparam>
    /// <param name="container">The application container.</param>
    /// <param name="transport">Selects the Rebus transport.</param>
    /// <param name="startOutboxProcessor">Whether this process drains the Rebus outbox.</param>
    /// <param name="configureOptions">Optional extra Rebus options, used by tests.</param>
    /// <param name="configureTest">Optional extra Rebus configuration (in-memory subscriptions, timeouts), used by tests.</param>
    public static void Configure<THost>(
        Container container,
        Action<StandardConfigurer<ITransport>> transport,
        bool startOutboxProcessor,
        Action<OptionsConfigurer>? configureOptions = null,
        Action<RebusConfigurer>? configureTest = null)
        where THost : IArkRebusHost
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(transport);
        var requirements = THost.GetRequirements();
        THost.Register((serviceType, implementationType) => container.Collection.Append(serviceType, implementationType));
        container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));
        container.RegisterSingleton(() => new RebusMessagingBus(
            container.GetInstance<global::Rebus.Bus.IBus>(),
            requirements.Identity,
            requirements.PublishedEventTypes));
        container.RegisterSingleton<IBus>(() => container.GetInstance<RebusMessagingBus>());
        container.RegisterSingleton<IBusOutboxEnlistment>(() => container.GetInstance<RebusMessagingBus>());
        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(t =>
            {
                transport(t);
                _configureOutbox(t, container, startOutboxProcessor);
            });
            _configureCommon(cfg, container, THost.ConfigureRouting, options =>
            {
                THost.ConfigureOptions(options);
                configureOptions?.Invoke(options);
            });
            configureTest?.Invoke(cfg);
        });
    }
```

Check `IArkRebusHost` for the exact static member names (`GetRequirements`,
`Register`, `ConfigureRouting`, `ConfigureOptions`) against the generated
`RebusProcessorHost` in `obj/…/generated`; adjust the call sites, not the
interface. If `IArkRebusHost` does not expose them as static abstract members,
make `Configure` take the four delegates instead of a type parameter.

Receivers register `IContextProvider<ClaimsPrincipal>` as
`RebusPrincipalContextWithFallbackProvider`; the WebInterface registers
`AspNetCoreUserContextProvider` (HTTP only — the one-way client receives
nothing).

- [ ] **Step 3: Processes**

- `…WebRebus.Processor`: from the moved `RebusProcessor`; `[ArkRebusHost(typeof(SampleMessagingParticipant))] public sealed partial class WorkerRebusHost;`
  `Program.cs` builds `RebusHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = … })`,
  `RebusHosting.Configure<WorkerRebusHost>(container, t => t.UseAzureServiceBus(conn, "ark-mediator-sample"), startOutboxProcessor: true)`,
  `container.Verify(); container.StartBus(); await WorkerRebusHost.SubscribeAsync(container.GetInstance<global::Rebus.Bus.IBus>());`
  and waits on `Host` lifetime as
  the Web processors do (generic host + `IHostedService` that disposes the
  container on stop).
- `…WebRebus.NotificationProcessor`/`…AuditProcessor`: same with their
  `ArkRebusHost` over `SampleMessagingNotificationParticipant` /
  `SampleMessagingAuditParticipant` and the matching
  `RegisterNotificationSubscriber`/`RegisterAuditSubscriber`; both call their
  host's `SubscribeAsync` after `StartBus`, which is what subscribes them to
  `BookPrintCompleted`.
- `…WebRebus.WebInterface`: copy of the Web variant's `SampleStartup` with the
  gRPC, MCP, MessagePack and messaging producer parts removed;
  `[ArkRebusHost(typeof(SampleMessagingApiParticipant))] public sealed partial class ApiRebusHost;`
  and `RebusHosting.Configure<ApiRebusHost>(container, t => t.UseAzureServiceBusAsOneWayClient(conn), startOutboxProcessor: false)`;
  `OnContainerVerified = c => c.StartBus()`.

Use the Rebus Azure Service Bus transport package already in
`Directory.Packages.props`; if none is listed, stop and ask (adding a package
needs approval).

- [ ] **Step 4: Remove Rebus from Application and API**

- Delete the three Rebus methods from `ApplicationComposition`.
- Remove `[RebusMessage(...)]` from `CreateBookReviewRequest.V1` and
  `ProcessBookPrintProcessRequest` (participant declarations are the source of
  truth; `[RebusMessage]` is only for the legacy assembly-scan path).
- Delete `DeadLetterContracts.cs`, `FailingRebusRequestHandler.cs`, the
  `FailingRebusRequest` registration, and the two `JsonSerializable` entries.
- Drop `Ark.Tools.MediatorFramework.Rebus` and `Ark.Tools.Outbox.Rebus` from
  Application; drop `Ark.Tools.MediatorFramework.Rebus` from API.
- Delete `S/src/Ark.MediatorFramework.Sample.RebusProcessor` remnants and the
  outbound-Rebus path of the old `AzureFunctions` host
  (`AzureFunctionsRebusComposition.cs`, the `EnableOutboundRebus` branch in its
  `Program.cs`, the outbound tests in `AzureFunctionsRebusTests.cs`, the Rebus
  package references in its csproj).
- Accept the API/Application `ArkApiSurface.txt` diff: only the removed
  `REBUS` lines and the removed `FailingRebusRequest`/`DeadLetterAck` contracts.

- [ ] **Step 5: Verify the boundary**

```bash
grep -rn "Rebus" $S/Core/Ark.MediatorFramework.Sample.Core.API $S/Core/Ark.MediatorFramework.Sample.Core.Application \
  --include=*.cs --include=*.csproj | grep -v '/obj/'
```

Expected: no output.

```bash
dotnet test $S/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Tests
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests
dotnet test $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/test/Ark.MediatorFramework.Sample.Tests
dotnet build Ark.Tools.slnx --configuration Debug
```

Expected: all green.

- [ ] **Step 6: README and commit**

`WR/README.md`: 4 processes, queues, Rebus outbox in each process, required
configuration, run commands. Then:

```bash
git add -A samples/Ark.MediatorFramework.Sample Ark.Tools.slnx
git commit -m "feat(samples): add web rebus host variant" -m "Assisted-by: Claude"
```

---

### Task 8: `Functions` variant and retirement of the old layout

**Files:**
- Create: `F/…Core.Functions.Hosting/` — `…Hosting.csproj`, `HttpHost.cs` (from old `Functions/FunctionGeneration.cs`), `FunctionsHosting.cs`, `MessagingPrincipalContextProvider.cs` (same code as Web's, own namespace)
- Create: `F/…Core.Functions.Api/` (from old `AzureFunctions`: `Program.cs`, `host.json`, `local.settings.json.example`), `F/…Core.Functions.Processor/`, `F/…Core.Functions.Notifications/` (from old `AzureFunctions` messaging trigger), `F/…Core.Functions.Audit/` (from old `AuditFunctions`), `F/…Core.Functions.OutboxProcessor/`
- Create: `F/…Core.Functions.Tests/` — native parts of `AzureFunctionsRebusTests.cs` renamed `FunctionsCompositionTests.cs`; new `ServiceBusDeliveryTests.cs`
- Create: `F/README.md`
- Delete: `S/src/` (now only old `AzureFunctions`, `AuditFunctions`, `Functions`), `S/test/` (old test project), `S/Ark.MediatorFramework.Sample.yml`, `.buildStage.yml`, `.deployStage.yml` (replaced in Task 9)
- Modify: `C/…Core.Application/Host/ApplicationComposition.cs` (delete the temporary legacy `Register` overload)
- Modify: `src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/Ark.Tools.MediatorFramework.Messaging.csproj` (remove `InternalsVisibleTo Include="Ark.MediatorFramework.Sample.Tests"`)
- Modify: `.vscode/settings.json` (`azureFunctions.projectSubpath` → `samples\\Ark.MediatorFramework.Sample\\Core\\Hosts\\Functions\\Ark.MediatorFramework.Sample.Core.Functions.Api`)

**Interfaces:**
- Produces: `FunctionsHosting.CreateContainer(ApplicationOptions options)`;
  `FunctionsHosting.DataBusOptions(IConfiguration configuration) : AzureBlobDataBusOptions`
  (same values as `WebHosting.CreateDataBus`: container `amf1-databus`, prefix
  `sample/`, 7-day minimum lifetime, `ConnectionStrings:DataBus` required) — one
  shared Blob store for every Functions app, because each app is a separate
  process and an in-memory DataBus would strand claim-checked payloads; `FunctionsHosting.AddUserContext(IServiceCollection services, Container container) : MessagingPrincipalContextProvider` — registers the principal provider in Microsoft DI **before** `AddArkAzureFunctions` (so its `TryAddSingleton` keeps ours and the SimpleInjector bridge forwards it) and the `UserContextIncomingStep`/`UserContextOutgoingStep` instances.

- [ ] **Step 1: Write the failing composition tests**

`FunctionsCompositionTests.cs`:

```csharp
[TestMethod]
public void ApiAppIsProducerOnly()
{
    // Build the Api Functions service collection exactly as Api/Program.cs does,
    // with an in-memory transport injected; assert no MessagingProcessorHost and
    // no MessagingTriggeredHostMarker registration, and that IBus is resolvable.
}

[TestMethod]
public void EachTriggerAppDeclaresOneParticipant()
{
    // For Processor, Notifications, Audit: read the assembly-level
    // MessagingFunctionsHostAttribute and assert the participant type:
    // SampleMessagingParticipant, SampleMessagingNotificationParticipant, SampleMessagingAuditParticipant.
}
```

Write both bodies concretely from the port of `AzureFunctionsRebusTests.NativeFunctionsCompositionBridgesScopedCommandProcessingIntoMicrosoftDependencyInjection`
(same service-collection construction, new participants). Run; expected:
build error (projects missing).

- [ ] **Step 2: Hosting library**

- `HttpHost.cs`: the `[assembly: HttpHost(typeof(Book_CreateRequest.V1), "/api/v{version}", ExcludedContracts = …)]`
  attribute from the old `Functions/FunctionGeneration.cs`, unchanged.
- `FunctionsHosting.CreateContainer` = Web's `CreateContainer`.
- `FunctionsHosting.AddUserContext`:

```csharp
    /// <summary>Registers message principal flow for a Functions app.</summary>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="container">The application container.</param>
    /// <returns>The registered provider.</returns>
    public static MessagingPrincipalContextProvider AddUserContext(IServiceCollection services, Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        var principal = new MessagingPrincipalContextProvider();
        services.AddSingleton<IContextProvider<ClaimsPrincipal>>(principal);
        services.AddSingleton(new UserContextIncomingStep(principal.Set));
        services.AddSingleton(new UserContextOutgoingStep(() => principal.Current));
        return principal;
    }
```

The Api app does not call `AddUserContext`: its HTTP principal comes from
`ArkAzureFunctionsUserContextProvider`; it registers only
`services.AddSingleton(sp => new UserContextOutgoingStep(() => sp.GetRequiredService<IContextProvider<ClaimsPrincipal>>().Current))`.

- [ ] **Step 3: Trigger apps**

Each of `Processor`, `Notifications`, `Audit` contains:

```csharp
[assembly: Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsHost(
    typeof(SampleMessagingParticipant),
    Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsTriggerBinding.ServiceBus,
    ConnectionConfigurationKey = "AzureServiceBus:ConnectionString",
    IncomingSteps = new[] { typeof(Ark.Tools.MediatorFramework.Messaging.UserContextIncomingStep) },
    OutgoingSteps = new[] { typeof(Ark.Tools.MediatorFramework.Messaging.UserContextOutgoingStep) })]
```

(participant type per app) and a `Program.cs` derived from the old
`AzureFunctions/Program.cs` with: `FunctionsHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = builder.Configuration.GetConnectionString("Sample") ?? throw … })`;
the subscriber registration for Notifications/Audit; `FunctionsHosting.AddUserContext`
before `AddArkAzureFunctions`; `AddArkSolidProcessors(container)` (bridges the scoped `ICommandProcessor`/`IRequestProcessor` and the handler verifier into MS DI, as the old Functions `Program.cs` does); then `ConfigureArkMessagingFunctions(... .UseTransport(t => t.UseServiceBus()).UseDataBus(d => d.UseAzureBlob(FunctionsHosting.DataBusOptions(builder.Configuration))).UseOutbox(o => o.UseEnqueue()))`;
`AddArkAzureFunctionsSimpleInjectorBridge(container)`; the authentication block
kept only in `Api`. These apps do not set `FunctionsInDependencies`, so they do
not expose the HTTP functions from the Hosting library.

- [ ] **Step 4: Api app**

From the old `AzureFunctions` project: `FunctionsInDependencies = true`,
reference `…Core.Functions.Hosting`; `Program.cs` keeps authentication,
health checks, App Insights, NLog; replaces the native composition with
`FunctionsHosting.CreateContainer(...)`, `AddArkAzureFunctions()`,
`AddArkSolidProcessors(container)`, then:

```csharp
var serviceBusConnection = builder.Configuration["AzureServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(serviceBusConnection))
    throw new InvalidOperationException("AzureServiceBus:ConnectionString is required.");
builder.Services.ConfigureArkMessaging(
    SampleMessagingNetwork.CreateOptions(),
    SampleMessagingNetwork.Registry,
    messaging => messaging.Producer<SampleMessagingApiParticipant>(p => p
        .UseTransport(t => t.UseServiceBus(new ServiceBusClient(serviceBusConnection)))
        .UseDataBus(d => d.UseAzureBlob(FunctionsHosting.DataBusOptions(builder.Configuration)))
        .UseOutgoingPipeline(typeof(UserContextOutgoingStep))
        .UseOutbox()));
builder.Services.AddArkAzureFunctionsSimpleInjectorBridge(container);
```

If composition rejects a producer inside a Functions host, stop: that is the
framework gap named in the spec's Risks. Record it as a framework task with a
failing test in `tests/Ark.Tools.MediatorFramework.Tests` and ask before
changing the framework.

- [ ] **Step 5: Outbox processor**

Copy the Web variant's `OutboxProcessor` into `F/…Core.Functions.OutboxProcessor`
(rename namespace). It is intentionally duplicated (spec D5).

- [ ] **Step 6: Port tests and delete the old layout**

```bash
OLD=$S/test/Ark.MediatorFramework.Sample.Tests
T=$S/Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Tests
git mv $OLD/AzureFunctionsRebusTests.cs $T/FunctionsCompositionTests.cs
git rm -r $S/src $S/test
git rm $S/Ark.MediatorFramework.Sample.yml $S/Ark.MediatorFramework.Sample.buildStage.yml $S/Ark.MediatorFramework.Sample.deployStage.yml
```

`MessagingBusSampleTests.cs` and `MessagingSourceTestExtensions.cs` are deleted
with the old project, not ported. Their send-routing, retry-boundary, and
fan-out checks are covered by the `Core.Tests` scenarios on real processor
hosts; their Storage Queue/Azurite checks do not apply to this variant, whose
trigger apps are compiled for Service Bus and whose network requires pub/sub
(Storage Queue transport behavior stays covered by
`tests/Ark.Tools.MediatorFramework.Tests`). Deleting
`MessagingSourceTestExtensions` removes the last use of framework internals,
so the `InternalsVisibleTo` entry can go.

Add `ServiceBusDeliveryTests.cs`. It follows the repository's emulator
convention (see `tests/Ark.Tools.MediatorFramework.Tests/ServiceBusMessagingTransportConformanceTests.cs`):
connection from `ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING`, defaulting to the
documented local emulator value exactly as that test class does; entities
created and deleted with `ServiceBusAdministrationClient`; no `Config.json`.
CI already runs the emulator service.

```csharp
[TestMethod]
public async Task ApiMessageReachesWorkerQueueThroughTheOutbox()
{
    var queue = FunctionsTestHosts.WorkerQueueName();
    var administration = new ServiceBusAdministrationClient(ServiceBusEmulator.AdministrationConnectionString);
    await ServiceBusEmulator.RecreateQueueAsync(administration, queue).ConfigureAwait(false);
    try
    {
        await using var client = new ServiceBusClient(ServiceBusEmulator.DataPlaneConnectionString);
        var store = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        await using var api = await FunctionsTestHosts.StartApiProducerAsync(client, store).ConfigureAwait(false);
        await using var outbox = await FunctionsTestHosts.StartOutboxProcessorAsync(client, store).ConfigureAwait(false);

        var book = await api.DispatchAsync<Book_CreateRequest.V1, Book.V1.Output>(FunctionsTestHosts.NewBook())
            .ConfigureAwait(false);
        await api.DispatchAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(
            new CreateBookPrintProcessRequest.V1 { BookId = book.Id }).ConfigureAwait(false);

        await using var receiver = client.CreateReceiver(queue);
        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        message.Should().NotBeNull();
        message!.ApplicationProperties[MessagingHeaders.MessageType].Should().Be("books_process_book_print_process");
        message.ApplicationProperties.Should().ContainKey(UserContextHeaderName);
    }
    finally
    {
        await administration.DeleteQueueAsync(queue).ConfigureAwait(false);
    }
}
```

- `ServiceBusEmulator` (test-only static class) mirrors
  `ServiceBusMessagingTransportConformanceTests`: `AdministrationConnectionString`
  is `ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING` or the documented default
  (HTTP administration port 5300); `DataPlaneConnectionString` is the same
  string with the endpoint port removed (copy `_dataPlaneConnectionString` and
  its `[ComplianceReviewed]` attribute verbatim). Administration clients use the
  first, `ServiceBusClient` uses the second.
- `StartApiProducerAsync` composes the Api app's service collection exactly as
  `Api/Program.cs` does (`FunctionsHosting.CreateContainer`, producer with
  `UseOutbox()` and `UseOutgoingPipeline(typeof(UserContextOutgoingStep))`),
  with `UseServiceBus(client)` and the in-memory DataBus (single-process test).
  It returns a dispatch facade like `ParticipantProcess` in Task 4 and sets an
  authenticated principal with all `ApplicationScopes`.
- `StartOutboxProcessorAsync` composes the same service collection as
  `Core.Functions.OutboxProcessor/Program.cs`
  (`AddArkMessagingOutboxProcessor(store, batchSize: 10)` over a
  `ServiceBusMessagingTransport` built from `client`) and starts its hosted
  service. The Api producer only commits envelopes to `store`'s outbox; this
  processor is what puts them on the queue.
- `WorkerQueueName()` returns the native entity name the Processor app's
  generated manifest binds to (read it from the generated
  `ArkGeneratedMessagingFunctions.Manifest` of the Processor project, or from
  `ServiceBusMessagingTransport`'s name mapping if it exposes one).
- `UserContextHeaderName` is the header `UserContextOutgoingStep` writes; take
  the constant from `Ark.Tools.MediatorFramework.Messaging` (check
  `UserContextMessagingSteps.cs`).

Settlement and dead-lettering of generated Service Bus triggers are framework
behavior (`ServiceBusMessageActions` is required by the generated function and
covered by the framework's Functions trigger tests); retry exhaustion to a dead
letter is covered for this application by the Core.Tests scenario
`Reject an unauthorized book review through the background bus`. This variant's
tests do not drive a real broker settlement loop.

Delete the legacy `Register(Container, bool, …)` overload from
`ApplicationComposition`. Remove the sample entry from `InternalsVisibleTo`.
Remove every old project from `Ark.Tools.slnx` and the sample slnx.

- [ ] **Step 7: Run everything**

```bash
dotnet test $S/Core/Hosts/Functions/Ark.MediatorFramework.Sample.Core.Functions.Tests   # needs the Service Bus emulator (CI service)
dotnet test $S/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.Tests
dotnet test $S/Core/Hosts/WebRebus/Ark.MediatorFramework.Sample.Core.WebRebus.Tests
ARK_SAMPLE_INMEMORY_TESTS=1 dotnet test $S/Core/Ark.MediatorFramework.Sample.Core.Tests
dotnet build Ark.Tools.slnx --configuration Debug
test ! -e $S/src && test ! -e $S/test && echo "old layout removed"
```

Expected: all green, last line prints `old layout removed`.

- [ ] **Step 8: README and commit**

`F/README.md`: 5 deployables, `local.settings.json` keys per app (copied from
the old examples, plus `ConnectionStrings__DataBus` = `UseDevelopmentStorage=true`
for Azurite in every app), `func start --port 7071..7074`, outbox processor run command.

```bash
git add -A samples/Ark.MediatorFramework.Sample Ark.Tools.slnx .vscode/settings.json \
  src/mediator-framework/Ark.Tools.MediatorFramework.Messaging/Ark.Tools.MediatorFramework.Messaging.csproj
git commit -m "feat(samples): add functions host variant and retire old layout" -m "Assisted-by: Claude"
```

---

### Task 9: Pipelines, documentation, and agent guidance

**Files:**
- Create: `S/Ark.MediatorFramework.Sample.Core.Web.yml`, `.buildStage.yml`, `.deployStage.yml`; same for `WebRebus` and `Functions` (9 files)
- Modify: `S/README.md`, `S/AGENTS.md`, `C/…Core.Application/AGENTS.md`
- Modify: `docs/mediator-framework/*.md` (every `Source:` link and sample path), `docs/mediator-framework/getting-started.md` (Ping `Source:` lines removed)
- Modify: `docs/design/mediator-framework/design.md` (*Sample mapping to this design*), `docs/design/mediator-framework/azure-functions-design.md`, `mcp-design.md`, `messaging-throughput-prd.md`, `docs/otel/upgrade-guide.md` (sample paths)
- Modify: `AGENTS.md` (generated-files example path)
- Modify: `docs/plans/mediator-framework/tasks/README.md` (mark this plan complete)

- [ ] **Step 1: Pipelines**

Base each variant's three files on the deleted
`Ark.MediatorFramework.Sample*.yml` (recover with
`git show HEAD~1:samples/Ark.MediatorFramework.Sample/Ark.MediatorFramework.Sample.buildStage.yml`).
Each build stage: restore locked, start SQL, build the root sample slnx, run
`Core.Tests` + the variant's tests, publish that variant's deployables plus the
DACPAC. The `Functions` build stage additionally starts the Service Bus emulator
(same image and environment as `.github/workflows/ci.yml`, pointed at the SQL
container), waits until ports 5300 and 5672 accept connections, and sets
`ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING` before running the Functions tests.
Each deploy stage keeps `enableDeployment: 'false'`. Triggers: keep the current
pipeline's branches, `master` and `develop`, for both push and PR, with path
filters `Core/*` + `Core/Hosts/<Variant>/*`.

- [ ] **Step 2: Rewrite links**

```bash
grep -rnE 'samples/Ark\.MediatorFramework\.Sample/(src|test)/' docs AGENTS.md samples/Ark.MediatorFramework.Sample --include=*.md
```

Map each hit: API/Application/Database → `Core/…Core.<Layer>/`; WebInterface,
GrpcClient, OutboxProcessor → `Core/Hosts/Web/…`; RebusProcessor →
`Core/Hosts/WebRebus/…Processor/`; AzureFunctions/AuditFunctions/Functions →
`Core/Hosts/Functions/…`; test project → `Core/…Core.Tests/` or the variant
test project that now holds the file. Remove `Source:` lines under Ping
snippets in `getting-started.md`. Re-run the grep; expected: no output.

- [ ] **Step 3: Verify every Markdown link**

```bash
python3 - <<'EOF'
import pathlib, re, sys
bad = []
for md in list(pathlib.Path('docs').rglob('*.md')) + list(pathlib.Path('samples/Ark.MediatorFramework.Sample').rglob('*.md')) + [pathlib.Path('AGENTS.md')]:
    for target in re.findall(r'\]\(([^)#\s]+)', md.read_text(encoding='utf-8')):
        if '://' in target or target.startswith('mailto:'):
            continue
        if not (md.parent / target).resolve().exists():
            bad.append(f'{md}: {target}')
print('\n'.join(bad)); sys.exit(1 if bad else 0)
EOF
```

Expected: exit 0. Fix any reported link (pre-existing broken links outside the
sample paths: mention them in the PR, do not fix).

- [ ] **Step 4: README, AGENTS, design doc**

- `S/README.md`: monorepo shape (service folders, `Common` slot), variant
  table with one line each and links to variant READMEs, shared test command,
  persistence profiles, eject steps (from ReferenceProject README).
- `S/AGENTS.md` + `Core.Application/AGENTS.md`: paths; rule "API and
  Application reference no host package"; rule "add a host by creating
  `Core/Hosts/<Variant>/` with a `Hosting` library and one project per
  participant".
- `design.md` *Sample mapping*: replace the WebInterface paragraph with a link
  to `sample-hosting-variants.md` and a three-line summary.
- Board: set the existing `SHV` row in `docs/plans/mediator-framework/tasks/README.md` to `Complete` (the section already exists).

- [ ] **Step 5: Commit**

```bash
git add -A docs AGENTS.md samples/Ark.MediatorFramework.Sample
git commit -m "docs(samples): document mediator sample hosting variants" -m "Assisted-by: Claude"
```

---

## Self-review against the spec

| Spec item | Task |
| --- | --- |
| D1 product monorepo, `Core` service | 2 |
| D2 variants under `Core/Hosts/<Variant>/` | 6, 7, 8 |
| D3 Web / WebRebus / Functions | 6, 7, 8 |
| D4 root slnx only | 2, 6–8 |
| D5 self-contained variants, duplicated process code | 6–8 (variant `Hosting` library; outbox processor duplicated) |
| D6 four participants, one process each | 4 (tests), 6, 7, 8 |
| D7 worker publishes `BookPrintCompleted` | 5 |
| Service Bus publisher provisioning outside Functions (framework gap) | 5b |
| D8 API/Application free of host packages | 7 (Rebus), 8 (legacy overload) |
| D9 host-neutral application tests | 4 |
| D10 configuration-driven composition, no test flags | 3, 6–8 |
| D11 retire old layout | 8 |
| Docs and pipelines | 9 |
| Local emulator connection in config only | 3, 6, 7, 8 |

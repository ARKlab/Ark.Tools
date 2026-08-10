---
name: ark-reqnroll
description: Writing Gherkin Feature files and the Reqnroll verb bindings steps. Use this skill when adding or reviewing Reqnroll features or Binding/Steps classes for any project.
---

# Reqnroll application scenarios

Use this skill when adding or reviewing Reqnroll features or Binding/Steps classes for any project.

## Workflow

1. Read the feature, bindings, hooks, drivers, and application contracts before
   adding a verb.
2. Prefer an existing coarse verb. Add a binding only when the action is
   reusable across scenarios or features.
3. Use a scenario-owned domain driver to dispatch application request, query,
   and command contracts. Keep URLs, status codes, JSON, and generated
   transport wrappers in focused transport tests.
4. Store active entities and results in the driver. Subsequent verbs should
   act on or compare that active state without repeating identifiers.
5. Use `Table` values when they improve readability: `CreateInstance<T>` for
   one DTO, `CreateSet<T>` for a collection, `CompareToInstance` for one
   result, and `CompareToSet` for result collections.
6. Run the affected feature with the project’s intended test profile. Include
   persistence and messaging infrastructure when the behavior owns it.

## Rules

- Make Gherkin describe a user or QA action, not a handler implementation.
- Keep bindings coarse and domain-neutral: create, update, retrieve, search,
  wait, and compare are reusable; do not add one verb per business case.
- Keep mutable state scenario-owned. Never share an active entity, container,
  clock, or client between scenarios.
- Steps classes are not per-Feature file and are supposed to be re-used even across features.
- Let drivers own domain state and contract dispatch; bindings should only map
  Gherkin input to driver calls and implement assertions. Drivers should swallow exceptions and track them for asserions to check.
- Model remote dependencies behind mock drivers. Do not mock
  application-owned infrastructure such as databases or message buses.
- For asynchronous workflows, build independent sender and receiver
  containers. Share only intentional scenario state and the test transport;
  never share a container or scope.
- Wait with bounded polling. Timeout messages should include enough queue,
  in-process, deferred, outbox, and error diagnostics to diagnose a failure.
- Dispose receivers, drain/reset test transport, clear queued work, and reset
  scenario data after every scenario.
- Keep public binding and driver types documented and follow the project’s
  source style.
- A `Given` setup binding may invoke the same operation as a `When`, but it must
  assert that the operation succeeded.
- Every scenario must finish with a `Then` assertion (or an `And` continuation
  of one); never leave the last action as an unchecked `When`.

## Scenario Example

```gherkin
Scenario: Update an active entity
    Given I create an entity with
        | Name |
        | Ada  |
    When I update the current entity with
        | Name        |
        | Ada Lovelace|
    Then the current entity is
        | Name        |
        | Ada Lovelace|


Scenario: Update an active entity with invalid name
    Given I create an entity with
        | Name |
        | Ada  |
    When I try to update the current entity with
        | Name        |
        | <Invalid>  |
    Then I get an error of type 'InvalidName'
```

```cs
public sealed class EntityDriver
{
    private readonly TestContext _ctx;

    public EntityDriver(TestContext ctx)
    {
        _ctx = ctx;
    }

    public Entity Current => _current ?? throw new InvalidOperationException("No current entity is available in this scenario.");

    private Entity? _current;

    public async Task CreateAsync(Entity.V1.Create input, CancellationToken ctk = default)
    {
        _current = await _ctx.DispatchRequestAsync<Entity_CreateRequest, Entity>(
            new Entity_CreateRequest(input), ctk).ConfigureAwait(false);
    }

    public async Task RetrieveCurrentAsync(CancellationToken ctk = default)
    {
        _current = await _ctx.DispatchQueryAsync<Entity_GetQuery, Entity>(
            new Entity_GetQuery(Current.Id),
            ctk).ConfigureAwait(false);
    }

    public async Task UpdateCurrentAsync(Entity.V1.Input input, CancellationToken ctk = default)
    {
        _current = await _ctx.DispatchRequestAsync<Entity_UpdateRequest, Entity>(
            new Entity_UpdateRequest(input, Current.Id),
            ctk).ConfigureAwait(false);
    }
}

```

```cs

[Binding]
public sealed class EntitySteps
{
    private readonly EntityDriver _entity;
    private Exception? _exception = null;

    public EntitySteps(EntityDriver entitys)
    {
        _entity = entitys;
    }

    [Given("I create a Entity with")]
    public async Task GivenCreateEntity(Table table)
    {
        await CreateEntity(table).ConfigureAwait(false);
        _entity.Current.Should().NotBeNull(); 
    }

    [When("I create a Entity with")]
    public async Task CreateEntity(Table table)
    {
        await _entity.CreateAsync(table.CreateInstance<Entity>()).ConfigureAwait(false);
    }
    
    [When("I try to create a Entity with")]
    public async Task CreateEntity(Table table)
    {
        await _try(CreateEntity(table));
    }

    [When("I retrieve the current Entity")]
    public async Task RetrieveCurrentEntity()
    {
        await _entity.RetrieveCurrentAsync().ConfigureAwait(false);
    }

    [When("I update the current Entity with")]
    public async Task UpdateCurrentEntity(Table table)
    {
        var merged = table.MergeInstance(_entity.Current);
        await _entity.UpdateCurrentAsync(new Entity
        {
            Title = merged.Title,
            Author = merged.Author,
            Genre = merged.Genre,
        }).ConfigureAwait(false);
    }

    [Then("I get an error of type '(.*)'")]
    public void CheckException(string type)
    {
        _exception.Should().BeOfType<BusinessRuleViolationException>()
          .Which.BusinessRuleViolation.Type.Should().Be(type);
    }

    private async Task _try(Func<Task> action)
    {
      try { await action() } catch (Exception e) { _exception = e; }
    }
}

```

The create binding maps input to a contract, dispatches it through the
scenario driver, and activates the response. Update and assertion bindings
use that active response instead of repeating its identifier or transport
details.


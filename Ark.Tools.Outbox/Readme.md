![image](http://www.ark-energy.eu/wp-content/uploads/ark-dark.png)
# Ark.Tools.Outbox

Implements the Outbox pattern to forward events/side-effects to a Bus in a Transactional way.

Currently integration with Rebus and Ark.Tools.Sql is provided using Ark.Tools.Outbox.Rebus and Ark.Tools.Outbox.SqlServer

## How to

### Configure Rebus Transport

```csharp
var container = new SimpleInjector.Container();
container.ConfigureRebus(c
    .Transport(t => {
        t.UseXXX(...);
        t.Outbox(o => {
            // this is used only by the Outbox processor, not on Send() or Publish()
            o.OutboxContextFactory(c => c.Use(() => container.GetInstance<IOutboxContext>());
        });
    });
```

### Safe Way (no hidden behaviours)

```csharp
async Task ExecuteAsync()
{

    using var ctx = _contextFactory();
    using var scope = _bus.EnlistInto(ctx);

    await ctx.ReadSomething();

    await ctx.SaveSomething(else);    

    _bus.Publish(new SomethingUpdatedEvent(else));

    // this must be completed before Committing
    await scope.CompleteAsync();
    ctx.Commit();
}
```

### Unsafe Way (easy pitfalls) 

Implement a Context that automatically creates a RebusTransactionScope that Complete() on Commit().

```csharp

class ApplicationContext : AbstractSqlContextWithOutbox<App>, IOutboxContext
{
    private RebusTransactionScope _rebusScope;

    public ApplicationContext(
            string connectionString, 
            IDbConnectionManager connManager, 
            IOutboxContextSqlConfig outboxConfig)
       : base(connManager.Get(connectionString), outboxConfig)
    {
        _resetRebusScope();
    }

    private void _resetRebusScope()
    {
        _rebusScope?.Dispose();
        _rebusScope = new RebusTransactionScope();
        _rebusScope.Enlist(this);
    }_

    public override void Commit()
    {
         _rebusScope.Complete();
         _resetRebusScope();
    }

    public override void Rollback()
    {
        _resetRebusScope();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _rebusScope.Dispose();
    }
}

```

The use is more straighforward, but has also pitfalls as the Context is reusable.

```csharp

async Task ExecuteAsync()
{
    // this context automatically enlist the Bus
    using var ctx = _contextFactory();

    await ctx.ReadSomething();

    await ctx.SaveSomething(else);    

    _bus.Publish(new SomethingUpdatedEvent(else));

    ctx.Commit();

    // WARNING!!! This doesn't get sent, unless another ctx.Commit() is issued!
    _bus.Send(new AnotherMessage());
}

```

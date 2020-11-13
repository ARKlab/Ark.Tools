![image](http://www.ark-energy.eu/wp-content/uploads/ark-dark.png)
# Ark.Tools.Outbox

Implements the Outbox pattern to forward events/side-effect to a Bus in a Transactional way.

Currently integration with Rebus and Ark.Tools.Sql is provided.

## How to

### Configure Rebus Transport

```csharp
var bus = Configure.With(...)
    .Transport(t => {
        t.UseXXX(...);
        t.Outbox(o => {
            o.OutboxContext(ctx => ctx.)
        })
    })
    .(...)
    .Start();
```
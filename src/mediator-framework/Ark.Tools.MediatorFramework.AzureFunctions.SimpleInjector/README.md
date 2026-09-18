# Ark.Tools.MediatorFramework.AzureFunctions.SimpleInjector

Opt-in SimpleInjector bridge for `Ark.Tools.MediatorFramework.AzureFunctions` consumers that still
compose their application with SimpleInjector.

`Ark.Tools.MediatorFramework.AzureFunctions` is container-agnostic: it resolves
`IRequestProcessor`/`IQueryProcessor`/`ICommandProcessor` and pipeline steps from Microsoft
dependency injection, and registers `IBus`/`IBusOutboxEnlistment` directly in the Functions
`IServiceCollection` when composing native messaging. Applications that keep SimpleInjector as
their handler/decorator container need those services surfaced inside their own container too.

```csharp
var container = new Container();
container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
// ... register handlers/decorators in `container` as usual ...

builder.Services
    .AddArkSolidProcessors(container) // Ark.Tools.Solid.SimpleInjector: bridges IxxxProcessor
    .AddArkAzureFunctions()
    .ConfigureArkMessagingFunctions(builder.Configuration, ArkGeneratedMessagingFunctions.Manifest, host =>
    {
        host.UseAzureServiceBus();
    })
    .AddArkAzureFunctionsSimpleInjectorBridge(container); // exposes IBus/IBusOutboxEnlistment/IContextProvider<ClaimsPrincipal> to `container`
```

Call `AddArkAzureFunctionsSimpleInjectorBridge` last, after `AddArkAzureFunctions` and any
`AddArkMessagingFunctionsHost`/`ConfigureArkMessagingFunctions` call, so it can detect which
Microsoft dependency injection registrations exist and mirror only those into the SimpleInjector
container. The SimpleInjector registrations are lazy singletons: the underlying Microsoft
dependency injection `IServiceProvider` is captured once the generic host starts, which always
happens before Azure Functions serves requests.

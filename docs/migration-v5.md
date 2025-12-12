# Migration to Ark.Tools v5

## Migrate from Specflow to Reqnroll (v5.1)

**Support for Specflow is no longer maintained and will be removed in next Major.**

The ReferenceProject has been migrated from Specflow to Reqnroll. For reasons you can read [this](https://reqnroll.net/news/2024/02/from-specflow-to-reqnroll-why-and-how/) but long story short: Specflow is no longer maintained.

`Ark.Tools.Reqnroll` replaces `Ark.Tools.Specflow`: the only changes are in `namespace`.

To migrate:
1. Replace `Techtalk.Specflow` to `Reqnroll` in `using`
1. Replace `Ark.Tools.Specflow` to `Ark.Tools.Reqnroll` in both `using` and `PackageReference`
1. Replace `Specflow.*` to `Reqnroll.*` in `PackageReference`
1. Replace `Table` with `DataTable` and `TableRow` with `DataTableRow`
1. Add [`reqnroll.json`](../samples/Core/Ark.Reference.Core.Tests/reqnroll.json) to test projects
1. Fix Verbs: Reqnroll defaults to Gherkin style parameters instead of Regex style thus it might be required to update verbs as described [here](https://docs.reqnroll.net/latest/guides/migrating-from-specflow.html#cucumber-expressions-support-compatibility-of-existing-expressions), specifying the regex start/end markers (^/$).

If you have more issues, please refer to the official [migration guide](https://docs.reqnroll.net/latest/guides/migrating-from-specflow.html)

## .NET 8.0

.NET SDK has been updated to .NET 8. AspNetCore packages only target .NET 8 (latest LTS).

## Deprecated .NET Framework, .NET Standard

.NET 8 is the minimum version going forward.

## Flurl v4 Migration Guide

In services that use `IFlurlClient`, the `IFlurlClientFactory` will be replaced with the new `IArkFlurlClientFactory`. The difference in services is that now the flurl clients must be manually disposed after use by implementing IDisposable. An example implementation can be found in the WebApplicationDemo in the `PostService`.

**Note:** To continue using Newtonsoft as the json serializer, there is a param `useNewtonsoftJson` in the `IArkFlurlClientFactory.Get()` method.

In the TestHost for initializing tests that use Flurl, we now use `ArkFlurlClientFactory` as the factory. To connect to the test server we extend the `DefaultFlurlClientFactory` as such:

```csharp
class TestServerFactory : DefaultFlurlClientFactory
{
    private readonly TestServer _server;

    public TestServerFactory(TestServer server)
    {
        _server = server;
    }

    public override HttpMessageHandler CreateInnerHandler()
    {
        return _server.CreateHandler();
    }
}
```

We then initialize the factory:

```csharp
_factory = new ArkFlurlClientFactory(new TestServerFactory(_server.GetTestServer()));
```

We then register the factory as usual:

```csharp
ctx.ScenarioContainer.RegisterFactoryAs<IFlurlClient>(c => _factory.Get(_baseUri));
```

An example can be found in the TestProject under `TestHost`.

## Rebus upgrade to v8

Rebus has been upgraded to v8. 

1. There is a Breaking Change on SecondLevelRetries where `IFailed<T>` no longer has the `Exception` object, but a serialization friendly `ExceptionInfo`. Use `exceptionInfo.ToException()` to obtain an exception: do note that the original `StackTrace` is in the `Exception.Message` and not in the `Exception.StackTrace`.
2. The `UseAzureServiceBusNativeDeliveryCount()` is deprecated in favor of native support by Rebus. Migrate to `UseAzureServiceBus(...).UseNativeMessageDeliveryCount()`.

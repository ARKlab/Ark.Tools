# Migration from v2 to v3

## BREAKING CHANGES

### Microsoft.AspNetCore v5

* Change `netcoreapp3.1` to `net5.0` on all projects referencing `Ark.Tools.AspNetCore.*` projects

### SQL Client

* **BREAKING:** from `System.Data.SqlClient` to `Microsoft.Data.SqlClient`
  * Remove any NuGet reference to `System.Data.SqlClient` and replace, where needed, with `Microsoft.Data.SqlClient`

### Flurl v3

* **BREAKING:** upgraded to Flurl v3
  * Most usages should be fine, but those that expected Flurl methods to return a `HttpMessageResponse`, now return `IFlurlResponse` **Disposable!**

### AspNetCore Startup Changes

* **BREAKING:** change to AspNetCore base Startup on `RegisterContainer()`
  * `RegisterContainer()` no longer takes `IApplicationBuilder` parameter but a `IServiceProvider` as the Container registration has been moved during `ConfigureServices()`
  * This affects mostly those cases where `IServiceProvider` was used to check for Tests overrides of mocked services
  * Use `IHostEnvironment` or `services.HasService` if possible instead of relying on `IServiceProvider`

### JSON Serialization Default

* **BREAKING:** change to AspNetCore Startups. Now defaults to `System.Text.Json` instead of `Newtonsoft.Json`.
  * Use the parameter `useNewtonsoftJson: true` of base constructor to keep old behaviour
  * Migrate to use `Ark.Tools.SystemTextJson.JsonPolymorphicConverter` instead of `Ark.Tools.NewtonsoftJson.JsonPolymorphicConverter`

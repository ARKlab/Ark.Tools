![image](http://www.ark-energy.eu/wp-content/uploads/ark-dark.png)
# Ark.Tools
This is a set of core libraries developed and maintained by Ark as a set of helper or extensions of the libraries Ark choose to use in their LOB applications.

## Getting Started
All libraries are provided in NuGet.

Support for .NET Framework 4.7.1,.NET Standard 2.x, .NET 5.0. 
Support for other frameworks is up-for-grabs ;)

## Quick Start
The main library used by Ark in its stack are

* [NodaTime](https://nodatime.org/)
* [SimpleInjector](https://simpleinjector.org/)
* [Polly](http://www.thepollyproject.org/)
* [Dapper](http://dapper-tutorial.net/)
* [AspNetCore](https://docs.microsoft.com/en-us/aspnet/core/)

If you want to learn more about each project, look the respective Readme when present or directly at code.
Documentation improvements are up-for-grabs ;)

## Upgrade to NLog v5 in Ark.Tools>=v4.5

In v4.5 has been revisited the NLog integration and helpers to make use of new features.

### NLog 'default' Configuration

The best way to configure NLog is

```cs
    Host.CreateDefaultBuilder(args)
        .ConfigureNLog()
        .ConfigureServices(...)
    ;
```

is equivalent to

```cs
    .ConfigureLogging((ctx,l) =>
    {
        var appName = Assembly.GetEntryAssembly().GetName().Name;
        NLogConfigurer
            .For(appName)
            // Load default settings from IConfiguration
            .WithDefaultTargetsAndRulesFromConfiguration(ctx.Configuration)
            .Apply();

        l.ClearProviders(); // remove all Microsoft providers
        l.AddNLog(); // sink all Microsoft.Logging logs to NLog
    })
```

`.WithDefaultTargetsAndRulesFromAppSettings()` and `.WithDefaultTargetsAndRulesFromCloudSettings()` exists for older Configuration sources.

The NLog auto-configurer expect the following settings:
- `NLog.Database` for SQL Server target. The table name is passed in as paramter to the configuration extension method.
- `NLog.Smtp` for the Mail target
   - `NLog:NotificationList` for the receipient address. 
   - **NEW** The sender address is taken from Smtp connection string `;From=noreply@example.com` or from `.ConfigureNLog(mailfrom:"me@myapp.com")` (defaults to `noreply@ark-energy.eu`)
- `NLog:SlackWebHook` for the Slack target. By default only `Fatal` and `LoggerName=Slack.*` are sent.
- `APPINSIGHTS_INSTRUMENTATIONKEY` or `ApplicationInsights:InstrumentationKey` for the ApplicationInsights target. By default only `>=Error` are sent.

### NLog Structured Logging

Logging represent a non trivial part of the CPU consumption of a running Assembly: strings are bad, concateneting them is costly.
Log Messages are also generally structured to present some context variables which are of interest.

`NLog@v4.5` introduced [StructuredLogging](https://github.com/NLog/NLog/wiki/How-to-use-structured-logging) template support.
`Ark.Tools@v4.5` (same version, just a coincidence...) supports writing these captured properties in ApplicationInsights and Database Targets.

StructuredLogging is also more performant of string interpolation: string interpolation (`$"Message {variable}"`) **SHALL NOT be used for Logging!**
String interpolation is always performed even usually disabled levels like `Trace` or `Debug` causing a performance loss. 
Additionally the variables are not captured and cannot be used for log analysis querying the JSON fields.

```cs
// BAD
_logger.Info($"Logon by {user} from {ip_address}");

// GOOD
_logger.Info("Logon by {user} from {ip_address}", user, ip_address); // ordered by position
```

### NLog Slack (>=v4.4)

Starting `Ark.Tools@v4.4` there is support for Logging to Slack via [WebHook](https://api.slack.com/messaging/webhooks).

The Configuration auto-loaders like `WithDefaultTargetsAndRulesFromConfiguration()` looks for a `NLog:SlackWebHook` and if non-empty configure to send Logs as chat message to Slack.
The default Rules are either:
- LoggerName="Slack.*" (created via `_slackLogger = LogManager.CreateLogger("Slack.MyStuff");`)
- Level==Fatal

### NLog ApplicationInsights

Starting `Ark.Tools@v4.5` there is support for Logging to ApplicationInsights.

The Configuration auto-loaders like `WithDefaultTargetsAndRulesFromConfiguration()` looks for the Microsoft's default settings like `APPINSIGHTS_INSTRUMENTATIONKEY` and `ApplicationInsights:InstrumentationKey`.

The default Rules to log any `Error` or `Fatal` to ApplicationInsights, including any `Exception` and StructuredLogging properties.

## Migrate from v2 to v3

- **BREAKING:** Microsoft.AspNetCore v5
   - change netcoreapp3.1 to net5.0 on all projects referencing Ark.Tools.AspNetCore.* projects
- **BREAKING:** from `System.Data.SqlClient` to `Microsoft.Data.SqlClient`
   - remove any Nuget reference to `System.Data.SqlClient` and replace, where needed, with `Microsoft.Data.SqlClient`
- **BREAKING:** upgraded to Flurl v3
   - most usages should be fine, but those that expected Flurl method to return a HttpMessageResponse, as not returns IFlurlResponse **Disposable!**
- **BREAKING:** change to AspNetCore base Startup on RegisterContainer()
   - RegisterContainer() no longer takes IApplicationBuilder parameter but a IServiceProvider as the Container registration has been moved during ConfigureServices()
   - this affects mostly those cases where IServiceProvider was used to check for Tests overrides of mocked services
   - Use IHostEnvironment or services.HasService if possible instead of relying on IServiceProvider
- **BREAKING:** change to AspNetCore Startups. Now defaults to System.Text.Json instead of Newtonsoft.Json. 
   - Use the parameter `useNewtonsoftJson: true` of base ctor to keep old behaviour
   - Migrate from the `Ark.Tools.SystemTextJson.JsonPolymorphicConverter` instead of `Ark.Tools.NewtonsoftJson.JsonPolymorphicConverter`

## Contributing
Feel free to send PRs or to raise issues if you spot them. We try our best to improve our libraries.
Please avoid adding more dependencies to 3rd party libraries.

## Links
* [Nuget](https://www.nuget.org/packages/MessagePack.NodaTime/)
* [Github](https://github.com/ARKlab/MessagePack)
* [Ark Energy](http://www.ark-energy.eu/)

## License
This project is licensed under the MIT License - see the [LICENSE](https://github.com/ARKlab/Ark.Tools/blob/master/LICENSE) file for details.

## Licence Claims
A part of this code is taken from StackOverflow or blogs or example. Where possible we included reference to original links 
but if you spot some missing Acknolegment please open an Issue right away.


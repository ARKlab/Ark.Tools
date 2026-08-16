# Migration to Ark.Tools v4.5

## NLog 'default' Configuration

In v4.5 has been revisited the NLog integration and helpers to make use of new features present in NLog v5.

The best way to configure NLog is:

```csharp
Host.CreateDefaultBuilder(args)
    .ConfigureNLog()
    .ConfigureServices(...)
;
```

This is equivalent to:

```csharp
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

`.WithDefaultTargetsAndRulesFromAppSettings()` and `.WithDefaultTargetsAndRulesFromCloudSettings()` exist for older Configuration sources.

The NLog auto-configurer expects the following settings:

* `NLog.Database` for SQL Server target. The table name is passed in as parameter to the configuration extension method.
* `NLog.Smtp` for the Mail target
  * `NLog:NotificationList` for the recipient address.
  * **NEW** The sender address is taken from Smtp connection string `;From=noreply@example.com` or from `.ConfigureNLog(mailfrom:"me@myapp.com")` (defaults to `noreply@ark-energy.eu`)
* `NLog:SlackWebHook` for the Slack target. By default only `Fatal` and `LoggerName=Slack.*` are sent.
* `APPINSIGHTS_INSTRUMENTATIONKEY` or `ApplicationInsights:InstrumentationKey` for the ApplicationInsights target. By default only `>=Error` are sent.

## NLog Structured Logging

Logging represents a non-trivial part of the CPU consumption of a running Assembly: strings are bad, concatenating them is costly.
Log Messages are also generally structured to present some context variables which are of interest.

`NLog@v4.5` introduced [StructuredLogging](https://github.com/NLog/NLog/wiki/How-to-use-structured-logging) template support.
`Ark.Tools@v4.5` (same version, just a coincidence...) supports writing these captured properties in ApplicationInsights and Database Targets.

StructuredLogging is also more performant than string interpolation: string interpolation (`$"Message {variable}"`) **SHALL NOT be used for Logging!**
String interpolation is always performed even for usually disabled levels like `Trace` or `Debug` causing a performance loss.
Additionally the variables are not captured and cannot be used for log analysis querying the JSON fields.

```csharp
// BAD
_logger.Info($"Logon by {user} from {ip_address}");

// GOOD
_logger.Info(CultureInfo.InvariantCulture, "Logon by {user} from {ip_address}", user, ip_address);
```

## NLog Slack (>=v4.4)

Starting `Ark.Tools@v4.4` there is support for Logging to Slack via [WebHook](https://api.slack.com/messaging/webhooks).

The Configuration auto-loaders like `WithDefaultTargetsAndRulesFromConfiguration()` looks for a `NLog:SlackWebHook` and if non-empty configure to send Logs as chat message to Slack.

The default Rules are either:

* LoggerName="Slack.*" (created via `_slackLogger = LogManager.CreateLogger("Slack.MyStuff");`)
* Level==Fatal

## NLog ApplicationInsights

Starting `Ark.Tools@v4.5` there is support for Logging to ApplicationInsights.

The Configuration auto-loaders like `WithDefaultTargetsAndRulesFromConfiguration()` looks for the Microsoft's default settings like `APPINSIGHTS_INSTRUMENTATIONKEY` and `ApplicationInsights:InstrumentationKey`.

The default Rules log any `Error` or `Fatal` to ApplicationInsights, including any `Exception` and StructuredLogging properties.

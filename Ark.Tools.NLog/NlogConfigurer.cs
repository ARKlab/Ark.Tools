// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.NLog.Slack;

using Microsoft.ApplicationInsights.NLogTarget;
using Microsoft.Data.SqlClient;

using NLog;
using NLog.Common;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using TargetPropertyWithContext = Microsoft.ApplicationInsights.NLogTarget.TargetPropertyWithContext;

namespace Ark.Tools.NLog
{
    public static class NLogConfigurer
    {
        public const string SlackTarget = "Ark.Slack";
        public const string ApplicationInsightsTarget = "Ark.ApplicationInsights";
        public const string ConsoleTarget = "Ark.Console";
        public const string FileTarget = "Ark.File";
        public const string DatabaseTarget = "Ark.Database";
        public const string MailTarget = "Ark.Mail";
        public const string MailFromDefault = "noreply@ark-energy.eu";

        public const string TextLineLayout = @"${longdate} ${pad:padding=5:inner=${level:uppercase=true}} ${pad:padding=-20:inner=${logger:shortName=true}} ${message}${onexception:${newline}${exception:format=ToString,Data}}";

        static NLogConfigurer()
        {
            LogManager.Setup()
                .SetupExtensions(b => b
                    .RegisterAssembly(typeof(Configurer).Assembly)
                    .RegisterAssembly(typeof(ActivityTraceLayoutRenderer).Assembly))
                ;

            // This has been added support NLog loggers output to Console during application initialization,
            // before Configuration is Read and Host is Built.
            InternalLogger.LogLevel = LogLevel.Warn;
            InternalLogger.LogToConsole = true;

            var appName = AppDomain.CurrentDomain.FriendlyName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";

            NLogConfigurer.For(appName)
                .WithConsoleTarget(false)
                .WithConsoleRule("*", global::NLog.LogLevel.Info)
                .Apply()
                ;
        }

        public static Configurer For(string appName)
        {
            return new Configurer(appName);
        }

        public static Configurer WithSlackDefaultRules(this Configurer @this)
        {
            return @this.WithSlackRule("*", LogLevel.Fatal)
                        .WithSlackRule("Slack.*", LogLevel.Trace, LogLevel.Error)
                        ;
        }

        public static Configurer WithApplicationInsightsDefaultRules(this Configurer @this)
        {
            return @this.WithApplicationInsightsRule("*", LogLevel.Error);
        }

        public record Config(
            string? SQLConnectionString = null,
            string? SQLTableName = null,
            string? SmtpConnectionString = null,
            string? MailTo = null,
            string? MailFrom = null,
            string? SlackWebhook = null,
            string? ApplicationInsightsInstrumentationKey = null,
            bool? EnableConsole = null,
            bool Async = true);


        public static Configurer WithArkDefaultTargetsAndRules(this Configurer @this, Config config)
        {
            if (config.EnableConsole != false)
            {
                @this
                    .WithConsoleTarget(config.Async)
                    .WithConsoleRule("*", _isProduction() ? LogLevel.Info : LogLevel.Trace);
            }

            if (!string.IsNullOrWhiteSpace(config.SQLConnectionString))
            {
                @this
                    .WithDatabaseTarget(config.SQLTableName ?? @this.AppName, config.SQLConnectionString!, config.Async)
                    .WithDatabaseRule("*", LogLevel.Info);
            }

            if (!string.IsNullOrWhiteSpace(config.SmtpConnectionString) && config.MailTo is not null)
            {
                @this
                    .WithMailTarget(config.MailFrom, config.MailTo, config.SmtpConnectionString!, async: false)
                    .WithMailRule("*", LogLevel.Fatal)
                    ;
            }

            if (!string.IsNullOrWhiteSpace(config.SlackWebhook))
            {
                @this.WithSlackDefaultTargetsAndRules(config.SlackWebhook!, config.Async);
            }

            if (!string.IsNullOrWhiteSpace(config.ApplicationInsightsInstrumentationKey))
            {
                @this.WithApplicationInsightsTargetsAndRules(config.ApplicationInsightsInstrumentationKey!, config.Async);
            }

            if (Debugger.IsAttached)
            {
                @this
                    .WithDebuggerTarget()
                    .WithDebuggerRule()
                    ;
            }

            return @this;
        }

        public static Configurer WithSlackDefaultTargetsAndRules(this Configurer @this, string slackwebhook, bool async = true)
        {
            @this.WithSlackTarget(slackwebhook, async)
                 .WithSlackDefaultRules();
            return @this;
        }

        public static Configurer WithApplicationInsightsTargetsAndRules(this Configurer @this, string instrumentationKey, bool async = true)
        {
            @this.WithApplicationInsightsTarget(instrumentationKey, async)
                 .WithApplicationInsightsDefaultRules();
            return @this;
        }

        [Obsolete("Use .WithDefaultTargetsAndRulesFromConfiguration() from Ark.Tools.NLog.Configuration. Beware to use connectionString:NLog.Smtp", true)]
        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailTo, bool async = true)
        {
            return @this;
        }

        [Obsolete("Use .WithDefaultTargetsAndRulesFromConfiguration() from Ark.Tools.NLog.Configuration. Beware to use connectionString:NLog.Smtp", true)]
        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailTo,
            string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl,
            bool async = true)
        {
            return @this;
        }

        private static bool _isProduction()
        {
            return _getEnvironment().Equals("Production", StringComparison.OrdinalIgnoreCase);
        }

        private static string _getEnvironment()
        {
            return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";
        }

        [Obsolete("Use .WithDefaultTargetsAndRulesFromConfiguration() from Ark.Tools.NLog.Configuration. Beware to use connectionString:NLog.Smtp", true)]
        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo, bool async = true)
        {
            return @this;
        }

        [Obsolete("Use .WithDefaultTargetsAndRulesFromConfiguration() from Ark.Tools.NLog.Configuration. Beware to use connectionString:NLog.Smtp", true)]
        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo,
            string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl,
            bool async = true)
        {
            return @this;
        }

        [Obsolete("Use .WithDefaultTargetsAndRulesFromConfiguration() from Ark.Tools.NLog.Configuration or .WithArkDefaultTargetsAndRules(). Beware to use connectionString:NLog.Smtp", true)]
        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo,
            string smtpConnectionString,
            bool async = true)
        {

            return @this;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Targets are Disposed by NLog")]
        public sealed class Configurer
        {
            internal LoggingConfiguration _config = new();

            public string AppName { get; }

            internal Configurer(string appName)
            {
                AppName = appName;
                GlobalDiagnosticsContext.Set("AppName", appName);
                // exclude Microsoft and System logging when NLog.Extensions.Logging is in use
                _config.AddRule(new LoggingRule()
                {
                    LoggerNamePattern = "System.*",
                    FinalMinLevel = LogLevel.Warn,
                });
                _config.AddRule(new LoggingRule()
                {
                    LoggerNamePattern = "Microsoft.*",
                    FinalMinLevel = LogLevel.Warn,
                });
                _config.AddRule(new LoggingRule()
                {
                    LoggerNamePattern = "Microsoft.Hosting.Lifetime.*",
                    FinalMinLevel = LogLevel.Info,
                });
            }

            #region debugger
            public Configurer WithDebuggerTarget()
            {
                _config.AddTarget("Debugger", new DebuggerTarget("Debugger")
                {
                    Layout = TextLineLayout
                });
                return this;
            }

            public Configurer WithDebuggerRule()
            {
                _config.AddRuleForAllLevels("Debugger");
                return this;
            }
            #endregion

            #region targets
            public Configurer WithSlackTarget(string slackwebhook, bool async = true)
            {
                var slackTarget = new SlackTarget
                {
                    WebHookUrl = slackwebhook,
                };
                _config.AddTarget(SlackTarget, async ? _wrapWithAsyncTargetWrapper(slackTarget) : slackTarget);
                return this;
            }
            public Configurer WithApplicationInsightsTarget(string instrumentationKey, bool async = true)
            {
                var target = new ApplicationInsightsTarget()
                {
                    InstrumentationKey = instrumentationKey,
                    ContextProperties = {
                        new TargetPropertyWithContext("Properties", new JsonLayout() {
                            ExcludeEmptyProperties = true,
                            IncludeGdc = true,
                            IncludeScopeProperties = true,
                            RenderEmptyObject = true,
                            IncludeEventProperties = true,
                            ExcludeProperties = {"Message","Exception"}
                        })
                    }
                };
                _config.AddTarget(ApplicationInsightsTarget, async ? _wrapWithAsyncTargetWrapper(target) : target);
                return this;
            }

            public Configurer WithConsoleTarget(bool async = true)
            {
                var consoleTarget = new ConsoleTarget();
                consoleTarget.WriteBuffer = async;
                consoleTarget.AutoFlush = !async;
                consoleTarget.Layout = TextLineLayout;

                _config.AddTarget(ConsoleTarget, async ? _wrapWithAsyncTargetWrapper(consoleTarget) : consoleTarget);
                return this;
            }

            public Configurer WithFileTarget(bool async = true)
            {
                var fileTarget = new FileTarget();

                fileTarget.Layout = TextLineLayout;
                fileTarget.FileName = @"${basedir}\Logs\Trace.log";
                fileTarget.KeepFileOpen = true;
                fileTarget.ConcurrentWrites = false;
                fileTarget.ArchiveFileName = @"${basedir}\Logs\Trace_{#}.log";
                fileTarget.ArchiveNumbering = ArchiveNumberingMode.DateAndSequence;
                fileTarget.ArchiveEvery = FileArchivePeriod.Day;
                fileTarget.MaxArchiveFiles = 30;
                fileTarget.ArchiveAboveSize = 10000000;
                fileTarget.ArchiveDateFormat = "yyyy-MM-dd";
                _config.AddTarget(FileTarget, async ? _wrapWithAsyncTargetWrapper(fileTarget) : fileTarget);

                return this;
            }

            public Configurer WithDatabaseTarget(string logTableName, string connectionString, bool async = true)
            {
                logTableName = logTableName.Replace("[", string.Empty).Replace("]", string.Empty).Replace('.', '_');

                try
                {
                    _ensureTableIsCreated(connectionString, logTableName);
                }
                catch (Exception ex)
                {
                    InternalLogger.Fatal(ex, "Failed to setup Ark Database Target. Database logging is disabled");
                    // continue setup the Target: it's not going to work but NLog handles it gracefully
                }


                var databaseTarget = new DatabaseTarget();
                databaseTarget.DBProvider = "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient"; // see https://github.com/NLog/NLog/wiki/Database-target#microsoftdatasqlclient-and-net-core
                databaseTarget.ConnectionString = connectionString;
                databaseTarget.KeepConnection = true;
                databaseTarget.CommandText = string.Format(CultureInfo.InvariantCulture, @"
INSERT INTO [dbo].[{0}]
( 
      [TimestampUtc]
    , [TimestampTz]
    , [LogLevel]
    , [Logger]
    , [Callsite]
    , [AppName]
    , [RequestID]
    , [ActivityId]
    , [Host]
    , [Message]
    , [ExceptionMessage]
    , [StackTrace]
    , [Properties]
) 
VALUES
(
      @TimestampUtc
    , @TimestampTz
    , @LogLevel
    , @Logger
    , @Callsite
    , @AppName
    , TRY_CONVERT(UNIQUEIDENTIFIER, @RequestID)
    , @ActivityId
    , @Host
    , @Message
    , @ExceptionMessage 
    , @StackTrace
    , @Properties
)
          ", logTableName);
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("TimestampUtc", @"${date:universalTime=true}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("TimestampTz", @"${date:format=dd-MMM-yyyy h\:mm\:ss.fff tt K}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("LogLevel", @"${level:uppercase=true}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Logger", @"${logger}"));
                // callsite is very-very expensive. Disable in Production.
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Callsite", _isProduction() ? "" : @"${when:when=level>=LogLevel.Error:inner=${callsite:filename=true}}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("AppName", "${scopeproperty:item=AppName:whenempty=${gdc:item=AppName}}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("RequestID", @"${mdlc:item=RequestID}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("ActivityId", "${activity:property=TraceId}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Properties", new JsonLayout()
                {
                    ExcludeEmptyProperties = true,
                    IncludeGdc = false, //false, due to NLog not respecting ExcludeProperties for GDC and we want to exclude AppName :(
                    IncludeScopeProperties = true,
                    RenderEmptyObject = true,
                    IncludeEventProperties = true,
                    ExcludeProperties = { "Message", "Exception", "AppName" }
                }));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Host", @"${ark.hostname}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Message", @"${message}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("ExceptionMessage", @"${onexception:${exception:format=Type,Message}}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("StackTrace", @"${onexception:${exception:format=ToString,Data}}"));
                _config.AddTarget(DatabaseTarget, async ? _wrapWithAsyncTargetWrapper(databaseTarget) : databaseTarget);

                return this;
            }

            private MailTarget _getBasicMailTarget()
            {
                var target = new MailTarget();
                target.AddNewLines = true;
                target.Encoding = Encoding.UTF8;
                target.Layout = TextLineLayout;
                target.Html = true;
                target.ReplaceNewlineWithBrTagInHtml = true;
                target.Subject = "Errors from ${scopeproperty:item=AppName:whenempty=${gdc:item=AppName}}@${ark.hostname}";

                return target;
            }

            public Configurer WithMailTarget(string to, bool async = true)
            {
                return this.WithMailTarget(null, to, async);
            }

            public Configurer WithMailTarget(string? from, string to, bool async = true)
            {
                var target = _getBasicMailTarget();

                target.From = from ?? NLogConfigurer.MailFromDefault;
                target.To = to;

                target.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                target.UseSystemNetMailSettings = true;

                _config.AddTarget(MailTarget, async ? _wrapWithAsyncTargetWrapper(target) : target);

                return this;
            }

            public Configurer WithMailTarget(string to, string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl, bool async = true)
            {
                return this.WithMailTarget(null, to, smtpServer, smtpPort, smtpUserName, smtpPassword, useSsl, async);
            }

            public Configurer WithMailTarget(string? from, string to, string? smtpServer, int? smtpPort, string? smtpUserName, string? smtpPassword, bool useSsl, bool async = true)
            {
                var target = _getBasicMailTarget();

                target.From = from;
                target.To = to;

                target.UseSystemNetMailSettings = false;
                target.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                target.EnableSsl = useSsl;
                target.SmtpAuthentication = SmtpAuthenticationMode.Basic;
                target.SmtpServer = smtpServer;
                target.SmtpPort = smtpPort;
                target.SmtpUserName = smtpUserName;
                target.SmtpPassword = smtpPassword;

                _config.AddTarget(MailTarget, async ? _wrapWithAsyncTargetWrapper(target) : target);

                return this;
            }

            public Configurer WithMailTarget(string? from, string to, string smtpConnectionString, bool async = true)
            {
                var cs = new SmtpConnectionBuilder(smtpConnectionString);
                return this.WithMailTarget(from ?? cs.From ?? NLogConfigurer.MailFromDefault, to, cs.Server, cs.Port, cs.Username, cs.Password, cs.UseSsl, async);
            }

            #endregion targets
            #region rules
            public Configurer WithSlackRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(SlackTarget);
                var ruleName = $"{SlackTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, level, target) { RuleName = ruleName, Final = final });

                return this;
            }
            public Configurer WithSlackRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(SlackTarget);
                var ruleName = $"{SlackTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { RuleName = ruleName, Final = final });

                return this;
            }

            public Configurer WithApplicationInsightsRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(ApplicationInsightsTarget);
                var ruleName = $"{ApplicationInsightsTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, level, target) { RuleName = ruleName, Final = final });

                return this;
            }
            public Configurer WithApplicationInsightsRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(ApplicationInsightsTarget);
                var ruleName = $"{ApplicationInsightsTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { RuleName = ruleName, Final = final });

                return this;
            }

            public Configurer WithConsoleRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(ConsoleTarget);
                var ruleName = $"{ConsoleTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, level, target) { RuleName = ruleName, Final = final });

                return this;
            }
            public Configurer WithConsoleRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(ConsoleTarget);
                var ruleName = $"{ConsoleTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { RuleName = ruleName, Final = final });

                return this;
            }

            public Configurer WithDatabaseRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(DatabaseTarget);
                var ruleName = $"{DatabaseTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, level, target) { RuleName = ruleName, Final = final });

                return this;
            }
            public Configurer WithDatabaseRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(DatabaseTarget);
                var ruleName = $"{DatabaseTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { RuleName = ruleName, Final = final });

                return this;
            }

            public Configurer WithFileRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(FileTarget);
                var ruleName = $"{FileTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, level, target) { RuleName = ruleName, Final = final });

                return this;
            }
            public Configurer WithFileRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(FileTarget);
                var ruleName = $"{FileTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { RuleName = ruleName, Final = final });

                return this;
            }

            public Configurer WithMailRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(MailTarget);
                var ruleName = $"{MailTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, level, target) { RuleName = ruleName, Final = final });

                return this;
            }
            public Configurer WithMailRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(MailTarget);
                var ruleName = $"{MailTarget}-{loggerPattern}";
                _config.RemoveRuleByName(ruleName);
                _config.AddRule(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { RuleName = ruleName, Final = final });

                return this;
            }

            #endregion

            public Configurer DisableMailRuleWhenInVisualStudio()
            {
                if (_isVisualStudioAttached())
                {
                    _config.RemoveRuleByName(MailTarget);
                }
                return this;
            }

            private bool _isVisualStudioAttached()
            {
                return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VisualStudioVersion")) || Debugger.IsAttached;
            }

            private static Target _wrapWithAsyncTargetWrapper(Target target)
            {
                var asyncTargetWrapper = new AsyncTargetWrapper();
                asyncTargetWrapper.WrappedTarget = target;
                asyncTargetWrapper.Name = target.Name;
                target.Name = target.Name + "_wrapped";
                return asyncTargetWrapper;
            }

            public void Apply(bool doNotDisableMailsInDebug = false)
            {
                if (doNotDisableMailsInDebug == false)
                    DisableMailRuleWhenInVisualStudio();

                LogManager.ThrowExceptions = _isVisualStudioAttached();
                LogManager.ThrowConfigExceptions = true;
                InternalLogger.LogToConsole = true;
                // this is last, so that ThrowConfigExceptions is respected on Config change
                LogManager.Configuration = _config;

                if (_isProduction())
                    LogManager.GlobalThreshold = LogLevel.Info;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0045:Do not use blocking calls in a sync method (need to make calling method async)", Justification = "Sync init method")]
        private static void _ensureTableIsCreated(string connString, string logTableName)
        {
            var creteLogTable = string.Format(@"
IF OBJECT_ID('[dbo].[{0}]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[{0}](
	    [ID] [int] IDENTITY(1,1) NOT NULL,
	    [TimestampUtc] [datetime2](7) NULL,
	    [LogLevel] [varchar](20) NULL,
	    [Logger] [varchar](256) NULL,
	    [AppName] [varchar](256) NULL,
	    [Message] [nvarchar](max) NULL,
	    [ExceptionMessage] [nvarchar](max) NULL,
	    [StackTrace] [nvarchar](max) NULL,
        [Properties] [nvarchar](max) NULL,
	    [Host] [varchar](256) NULL,
	    [TimestampTz] [datetimeoffset](7) NULL,
	    [Callsite] [varchar](8000) NULL,
	    [ActivityId] [varchar](256) NULL,
        [RequestID] [uniqueidentifier] NULL
    CONSTRAINT [{0}_PK] PRIMARY KEY CLUSTERED 
    (
	    [ID] DESC
    ) WITH (DATA_COMPRESSION = PAGE)
    )
END

IF NOT EXISTS ( SELECT  1
                FROM    information_schema.COLUMNS
                WHERE   table_schema = 'dbo'
                        AND TABLE_NAME = '{0}'
						AND COLUMN_NAME = 'ActivityId'
                        )
BEGIN

    EXEC('ALTER TABLE [dbo].[{0}] ADD [ActivityId] [varchar](256) NULL')

END

IF NOT EXISTS ( SELECT  1
                FROM    information_schema.COLUMNS
                WHERE   table_schema = 'dbo'
                        AND TABLE_NAME = '{0}'
						AND COLUMN_NAME = 'Properties'
                        )
BEGIN

    EXEC('ALTER TABLE [dbo].[{0}] ADD [Properties] [nvarchar](MAX) NULL')

END

IF NOT EXISTS ( SELECT  1
                FROM    information_schema.COLUMNS
                WHERE   table_schema = 'dbo'
                        AND TABLE_NAME = '{0}'
						AND COLUMN_NAME = 'StackTrace'
                        )
BEGIN

    EXEC('ALTER TABLE [dbo].[{0}] ADD [StackTrace] [nvarchar](MAX) NULL')

END


IF EXISTS ( SELECT  1
                FROM    information_schema.COLUMNS
                WHERE   table_schema = 'dbo'
                        AND TABLE_NAME = '{0}'
						AND COLUMN_NAME = 'Callsite'
						AND CHARACTER_MAXIMUM_LENGTH = 256
                        )
BEGIN

    -- limiting 'migration' to 8000 instead of MAX to ensure is only a metadata update. 
    -- (max) requires data move and long time
    EXEC('ALTER TABLE [dbo].[{0}] ALTER COLUMN [Callsite] [varchar](8000) NULL')

END

            ", logTableName);
            using var conn = new SqlConnection(connString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = creteLogTable;
            cmd.CommandTimeout = 180;
            cmd.ExecuteNonQuery();
        }


        internal sealed class STJSerializer : IJsonConverter
        {

            /// <summary>Serialization of an object into JSON format.</summary>
            /// <param name="value">The object to serialize to JSON.</param>
            /// <param name="builder">Output destination.</param>
            /// <returns>Serialize succeeded (true/false)</returns>
            public bool SerializeObject(object value, StringBuilder builder)
            {
                try
                {
                    builder.Append(JsonSerializer.Serialize(value, ArkSerializerOptions.JsonOptions));
                    return true;
                }
                catch (Exception e)
                {
                    InternalLogger.Error(e, "Error when serializing type '{type}' '{string}' as json.", value?.GetType(), value?.ToString());
                    return false;
                }
            }
        }
    }


}

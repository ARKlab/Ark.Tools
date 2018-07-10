// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using NLog.Config;
using NLog.Targets;
using System.Data.SqlClient;
using Dapper;
using NLog.Targets.Wrappers;
using System.Text;
using System;
using System.Linq;
using NLog.Common;

namespace Ark.Tools.NLog
{ 

    public static class NLogConfigurer
    {
        public const string ConsoleTarget = "Ark.Console";
        public const string FileTarget = "Ark.File";
        public const string DatabaseTarget = "Ark.Database";
        public const string MailTarget = "Ark.Mail";
        public const string MailFromDefault = "noreply@ark-energy.eu";

        public static Configurer For(string appName)
        {
            return new Configurer(appName);
        }

        public static Configurer WithDefaultTargets(this Configurer @this, string logTableName, string connectionString, string mailTo, bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTarget(logTableName, connectionString, async)
                        .WithMailTarget(mailTo, async: false)
                        ;
        }

        public static Configurer WithDefaultTargets(this Configurer @this, string logTableName, string connectionString, string mailTo,
            string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl,
            bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTarget(logTableName, connectionString, async)
                        .WithMailTarget(mailTo, smtpServer, smtpPort, smtpUserName, smtpPassword, useSsl, async: false)
                        ;
        }

        public static Configurer WithDefaultTargets(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo, bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTarget(logTableName, connectionString, async)
                        .WithMailTarget(mailFrom, mailTo, async: false)
                        ;
        }


        public static Configurer WithDefaultTargets(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo,
            string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl,
            bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTarget(logTableName, connectionString, async)
                        .WithMailTarget(mailFrom, mailTo, smtpServer, smtpPort, smtpUserName, smtpPassword, useSsl, async: false)
                        ;
        }

        public static Configurer WithDefaultTargets(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo,
            string smtpConnectionString,
            bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTarget(logTableName, connectionString, async)
                        .WithMailTarget(mailFrom, mailTo, smtpConnectionString, async: false)
                        ;
        }

        public static Configurer WithDefaultRules(this Configurer @this)
        {
            return @this.WithConsoleRule("*", LogLevel.Trace)
                        .WithFileRule("*", LogLevel.Trace)
                        .WithDatabaseRule("*", LogLevel.Info)
                        .WithMailRule("*", LogLevel.Fatal)
                        ;
        }

        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailTo, bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargets(logTableName, connectionString, mailTo, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();
            return @this;
        }

        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailTo,
            string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl,
            bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargets(logTableName, connectionString, mailTo, smtpServer, smtpPort, smtpUserName, smtpPassword, useSsl, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();

            return @this;
        }

        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo, bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargets(logTableName, connectionString, mailFrom, mailTo, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();
            return @this;
        }

        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo,
            string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl,
            bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargets(logTableName, connectionString, mailFrom, mailTo, smtpServer, smtpPort, smtpUserName, smtpPassword, useSsl, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();

            return @this;
        }

        public static Configurer WithDefaultTargetsAndRules(this Configurer @this, string logTableName, string connectionString, string mailFrom, string mailTo,
            string smtpConnectionString,
            bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargets(logTableName, connectionString, mailFrom, mailTo, smtpConnectionString, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();

            return @this;
        }

        public class Configurer
        {
            private LoggingConfiguration _config = new LoggingConfiguration();
            private readonly string _appName;
            private bool _throwExceptions = false;

            internal Configurer(string appName)
            {
                _appName = appName;
                ConfigurationItemFactory.Default.RegisterItemsFromAssembly(typeof(Configurer).Assembly);
            }

            #region targets
            public Configurer WithConsoleTarget(bool async = true)
            {
                var consoleTarget = new ColoredConsoleTarget();
                consoleTarget.Layout = @"${longdate} ${pad:padding=5:inner=${level:uppercase=true}} ${pad:padding=-20:inner=${logger:shortName=true}} ${message} ${onexception:${newline}EXCEPTION\: ${exception:format=ToString}}";
                _config.AddTarget(ConsoleTarget, async ? _wrapWithAsyncTargetWrapper(consoleTarget) as Target : consoleTarget);
                return this;
            }

            public Configurer WithFileTarget(bool async = true)
            {
                var fileTarget = new FileTarget();

                fileTarget.Layout = @"${longdate} - ${callsite} - ${level:uppercase=true}: ${message}${onexception:${newline}EXCEPTION\: ${exception:format=ToString}}";
                fileTarget.FileName = @"${basedir}\Logs\Trace.log";
                fileTarget.KeepFileOpen = true;
                fileTarget.ConcurrentWrites = false;
                fileTarget.ArchiveFileName = @"${basedir}\Logs\Trace_{#}.log";
                fileTarget.ArchiveNumbering = ArchiveNumberingMode.DateAndSequence;
                fileTarget.ArchiveEvery = FileArchivePeriod.Day;
                fileTarget.MaxArchiveFiles = 30;
                fileTarget.ArchiveAboveSize = 10000000;
                fileTarget.ArchiveDateFormat = "yyyy-MM-dd";
                _config.AddTarget(FileTarget, async ? _wrapWithAsyncTargetWrapper(fileTarget) as Target : fileTarget);

                return this;
            }

            public Configurer WithDatabaseTarget(string logTableName, string connectionString, bool async = true)
            {
                _ensureTableIsCreated(connectionString, logTableName);
                var databaseTarget = new DatabaseTarget();
                databaseTarget.ConnectionString = connectionString;
                databaseTarget.KeepConnection = true;
                databaseTarget.CommandText = string.Format(@"
INSERT INTO[dbo].[{0}]
( 
      [TimestampUtc]
    , [TimestampTz]
    , [LogLevel]
    , [Logger]
    , [Callsite]
    , [AppName]
    , [RequestID]
    , [Host]
    , [Message]
    , [ExceptionMessage]
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
    , @Host
    , @Message
    , @ExceptionMessage 
)
          ", logTableName);
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("TimestampUtc", @"${date:universalTime=true}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("TimestampTz", @"${date:format=dd-MMM-yyyy h\:mm\:ss.fff tt K}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("LogLevel", @"${level:uppercase=true}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Logger", @"${logger}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Callsite", @"${callsite:filename=true}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("AppName", _appName));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("RequestID", @"${mdlc:item=RequestID}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Host", @"${machinename}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("Message", @"${message}"));
                databaseTarget.Parameters.Add(new DatabaseParameterInfo("ExceptionMessage", @"${onexception:${exception:format=ToString}}"));
                _config.AddTarget(DatabaseTarget, async ? _wrapWithAsyncTargetWrapper(databaseTarget) as Target : databaseTarget);

                return this;
            }

            private MailTarget _getBasicMailTarget()
            {
                var target = new MailTarget();
                target.AddNewLines = true;
                target.Encoding = Encoding.UTF8;
                target.Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${newline}${exception:format=ToString:innerFormat=ToString:maxInnerExceptionLevel=10}";
                target.Html = true;
                target.ReplaceNewlineWithBrTagInHtml = true;
                target.Subject = "Errors from " + _appName + "@${ark.hostname}";

                return target;
            }

            public Configurer WithMailTarget(string to, bool async = true)
            {
                return this.WithMailTarget(NLogConfigurer.MailFromDefault, to, async);
            }

            public Configurer WithMailTarget(string from, string to, bool async = true)
            {
                var target = _getBasicMailTarget();

                target.From = from;
                target.To = to;

                target.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                target.UseSystemNetMailSettings = true;
                
                _config.AddTarget(MailTarget, async ? _wrapWithAsyncTargetWrapper(target) as Target : target);

                return this;
            }

            public Configurer WithMailTarget(string to, string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl, bool async = true)
            {
                return this.WithMailTarget(NLogConfigurer.MailFromDefault, to, smtpServer, smtpPort, smtpUserName, smtpPassword, useSsl, async);
            }

            public Configurer WithMailTarget(string from, string to, string smtpServer, int smtpPort, string smtpUserName, string smtpPassword, bool useSsl, bool async = true)
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

                _config.AddTarget(MailTarget, async ? _wrapWithAsyncTargetWrapper(target) as Target : target);

                return this;
            }

            public Configurer WithMailTarget(string from, string to, string smtpConnectionString, bool async = true)
            {
                var target = _getBasicMailTarget();

                target.From = from;
                target.To = to;

                var cs = new SmtpConnectionBuilder(smtpConnectionString);

                target.UseSystemNetMailSettings = false;
                target.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                target.EnableSsl = cs.UseSsl;
                target.SmtpAuthentication = SmtpAuthenticationMode.Basic;
                target.SmtpServer = cs.Server;
                target.SmtpPort = cs.Port;
                target.SmtpUserName = cs.Username;
                target.SmtpPassword = cs.Password;

                _config.AddTarget(MailTarget, async ? _wrapWithAsyncTargetWrapper(target) as Target : target);

                return this;
            }

            #endregion targets
            #region rules

            public Configurer WithConsoleRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(ConsoleTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, level, target) { Final = final });

                return this;
            }
            public Configurer WithConsoleRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(ConsoleTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { Final = final });

                return this;
            }

            public Configurer WithDatabaseRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(DatabaseTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, level, target) { Final = final });

                return this;
            }
            public Configurer WithDatabaseRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(DatabaseTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { Final = final });

                return this;
            }

            public Configurer WithFileRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(FileTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, level, target) { Final = final });

                return this;
            }
            public Configurer WithFileRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(FileTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, minLevel, maxLevel, target) { Final = final });

                return this;
            }

            public Configurer WithMailRule(string loggerPattern, LogLevel level, bool final = false)
            {
                var target = _config.FindTargetByName(MailTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, level, target) { Final = final });

                return this;
            }
            public Configurer WithMailRule(string loggerPattern, LogLevel minLevel, LogLevel maxLevel, bool final = false)
            {
                var target = _config.FindTargetByName(MailTarget);
                _config.LoggingRules.Add(new LoggingRule(loggerPattern, minLevel, maxLevel,  target) { Final = final });

                return this;
            }

            #endregion

            public Configurer ThrowInternalExceptionsInVisualStudio()
            {
                _throwExceptions = _isVisualStudioAttached();
                return this;
            }

            public Configurer DisableMailRuleWhenInVisualStudio()
            {
                if (_isVisualStudioAttached())
                {
                    var mt = _config.FindTargetByName(MailTarget);

                    var rules = _config.LoggingRules.Where(r => r.Targets.Contains(mt)).ToList();
                    foreach (var r in rules)
                        _config.LoggingRules.Remove(r);
                }
                return this;
            }

            private bool _isVisualStudioAttached()
            {
                return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VisualStudioVersion"));
            }

            private static Target _wrapWithAsyncTargetWrapper(Target target)
            {
                var asyncTargetWrapper = new AsyncTargetWrapper();
                asyncTargetWrapper.WrappedTarget = target;
                asyncTargetWrapper.Name = target.Name;
                target.Name = target.Name + "_wrapped";
                return asyncTargetWrapper;
            }

            public void Apply()
            {
                LogManager.Configuration = _config;
                LogManager.ThrowExceptions = _throwExceptions;
                LogManager.ThrowConfigExceptions = true;
                InternalLogger.LogToTrace = true;
            }
        }

        private static void _ensureTableIsCreated(string connString, string logTableName)
        {
            var creteLogTable = string.Format(@"
IF OBJECT_ID('{0}', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[{0}](
	    [ID] [int] IDENTITY(1,1) NOT NULL,
	    [TimestampUtc] [datetime2](7) NULL,
	    [TimestampTz] [datetimeoffset](7) NULL,
	    [LogLevel] [varchar](20) NULL,
	    [Logger] [varchar](256) NULL,
	    [Callsite] [varchar](256) NULL,
	    [AppName] [varchar](256) NULL,
	    [RequestID] [uniqueidentifier] NULL,
	    [Host] [varchar](256) NULL,
	    [Message] [nvarchar](max) NULL,
	    [ExceptionMessage] [nvarchar](max) NULL,
    CONSTRAINT [{0}_PK] PRIMARY KEY CLUSTERED 
    (
	    [ID] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, DATA_COMPRESSION = PAGE)
    )
END 
            ", logTableName);
            using (var conn = new SqlConnection(connString))
            {
                conn.Execute(creteLogTable);
            }
        }

    }


}

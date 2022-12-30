// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using System;
using System.Reflection;

namespace Ark.Tools.NLog
{
    using static Ark.Tools.NLog.NLogConfigurer;

    public static class NLogConfigurerConfiguration
    {
        public static Configurer WithDefaultTargetsAndRulesFromConfiguration(this Configurer @this, IConfiguration cfg, bool async = true)
        {
            var config = new Config
            {
                SQLConnectionString = cfg.GetNLogSetting("ConnectionStrings:" + NLogDefaultConfigKeys.SqlConnStringName),
                SmtpConnectionString = cfg.GetNLogSetting("ConnectionStrings:" + NLogDefaultConfigKeys.SmtpConnStringName),
                MailTo = cfg.GetNLogSetting(NLogDefaultConfigKeys.MailNotificationAddresses),
                ApplicationInsightsInstrumentationKey = cfg["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? cfg["ApplicationInsights:InstrumentationKey"],
                SlackWebhook = cfg.GetNLogSetting(NLogDefaultConfigKeys.SlackWebHook),
                Async = async
            };

            @this.WithArkDefaultTargetsAndRules(config);

            return @this;
        }

        public static string? GetNLogSetting(this IConfiguration cfg, string key)
        {
            // 1. Settings consts are defined with '.' separator for hierarchy (historical)
            // 2. For AppSettings we need to replace '.' with ':'
            // 3. For ConnectionStrings we need to try both with '.' and with '_' because
            //    On Windows hosting '.' is permitted 
            //    On Linux hosting '.' is replaced by Azure with '_'
            var res = cfg[key];
            if (res != null) return res;

            res = cfg[key.Replace('.', ':')];
            if (res != null) return res;

            res = cfg[key.Replace('.', '_')];
            if (res != null) return res;
            
            return res;
        }

        public static Configurer WithDefaultTargetsAndRulesFromConfiguration(this Configurer @this, IConfiguration cfg, string logTableName, string? mailFrom = null, bool async = true)
        {
            var config = new Config
            {
                SQLConnectionString = cfg.GetNLogSetting("ConnectionStrings:" + NLogDefaultConfigKeys.SqlConnStringName),
                SQLTableName = logTableName,
                SmtpConnectionString = cfg.GetNLogSetting("ConnectionStrings:" + NLogDefaultConfigKeys.SmtpConnStringName),
                MailTo = cfg.GetNLogSetting(NLogDefaultConfigKeys.MailNotificationAddresses),
                MailFrom = mailFrom,
                ApplicationInsightsInstrumentationKey = cfg["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? cfg["ApplicationInsights:InstrumentationKey"],
                SlackWebhook = cfg.GetNLogSetting(NLogDefaultConfigKeys.SlackWebHook),
                Async = async
            };

            @this.WithArkDefaultTargetsAndRules(config);

            return @this;
        }

        public static IHostBuilder ConfigureNLog(this IHostBuilder builder, string? appName = null, string? mailFrom = null)
        {
            appName ??= Assembly.GetEntryAssembly()?.GetName().Name ?? AppDomain.CurrentDomain.FriendlyName ?? "Unknown";

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                global::NLog.LogManager.GetLogger("Main").Fatal(e.ExceptionObject as Exception, "UnhandledException");
                global::NLog.LogManager.Flush();
            };

            return builder.ConfigureLogging((ctx, logging) =>
            {
                NLogConfigurer.For(appName)
                   .WithDefaultTargetsAndRulesFromConfiguration(ctx.Configuration, appName, mailFrom, async: !ctx.HostingEnvironment.IsEnvironment("SpecFlow"))
                   .Apply();

                logging.ClearProviders();
                logging.AddNLog();
            });

        }
    }
}

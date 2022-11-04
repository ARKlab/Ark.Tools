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
                SQLConnectionString = cfg.GetConnectionString(NLogDefaultConfigKeys.SqlConnStringName),
                SmtpConnectionString = cfg.GetConnectionString(NLogDefaultConfigKeys.SmtpConnStringName),
                MailTo = cfg[NLogDefaultConfigKeys.MailNotificationAddresses.Replace('.', ':')],
                ApplicationInsightsInstrumentationKey = cfg["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? cfg["ApplicationInsights:InstrumentationKey"],
                SlackWebhook = cfg[NLogDefaultConfigKeys.SlackWebHook.Replace('.', ':')],
                Async = async
            };

            @this.WithArkDefaultTargetsAndRules(config);

            return @this;
        }

        public static Configurer WithDefaultTargetsAndRulesFromConfiguration(this Configurer @this, IConfiguration cfg, string logTableName, string mailFrom = null, bool async = true)
        {
            var config = new Config
            {
                SQLConnectionString = cfg.GetConnectionString(NLogDefaultConfigKeys.SqlConnStringName),
                SQLTableName = logTableName,
                SmtpConnectionString = cfg.GetConnectionString(NLogDefaultConfigKeys.SmtpConnStringName),
                MailTo = cfg[NLogDefaultConfigKeys.MailNotificationAddresses.Replace('.', ':')],
                MailFrom = mailFrom,
                ApplicationInsightsInstrumentationKey = cfg["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? cfg["ApplicationInsights:InstrumentationKey"],
                SlackWebhook = cfg[NLogDefaultConfigKeys.SlackWebHook.Replace('.', ':')],
                Async = async
            };

            @this.WithArkDefaultTargetsAndRules(config);

            return @this;
        }

        public static IHostBuilder ConfigureNLog(this IHostBuilder builder, string appName = null, string mailFrom = null)
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

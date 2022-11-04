// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Configuration;


namespace Ark.Tools.NLog
{
    using static Ark.Tools.NLog.NLogConfigurer;

    public static class NLogConfigurerConfigurationManager
    {

        public static Configurer WithDefaultTargetsAndRulesFromAppSettings(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true)
        {
            var smtp = ConfigurationManager.ConnectionStrings[NLogDefaultConfigKeys.SmtpConnStringName].ConnectionString 
                ?? new SmtpConnectionBuilder()
                {
                    Server = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpServer],
                    Port = int.Parse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPort]),
                    Username = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUserName],
                    Password = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPassword],
                    UseSsl = bool.Parse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUseSsl])
                }.ConnectionString;

            var config = new Config
            {
                SQLConnectionString = ConfigurationManager.ConnectionStrings[NLogDefaultConfigKeys.SqlConnStringName].ConnectionString,
                SQLTableName = logTableName,
                SmtpConnectionString = smtp,
                MailTo = mailTo ?? ConfigurationManager.AppSettings[NLogDefaultConfigKeys.MailNotificationAddresses],
                MailFrom = mailFrom,
                SlackWebhook = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SlackWebHook],
                ApplicationInsightsInstrumentationKey = ConfigurationManager.AppSettings["APPINSIGHTS_INSTRUMENTATIONKEY"] 
                    ?? ConfigurationManager.AppSettings["ApplicationInsights:InstrumentationKey"],
                Async = async
            };

            @this.WithArkDefaultTargetsAndRules(config);

            return @this;
        }
    }
}

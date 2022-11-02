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
            var smtp = new SmtpConnectionBuilder(ConfigurationManager.ConnectionStrings[NLogDefaultConfigKeys.SmtpConnStringName].ConnectionString)
                ?? new SmtpConnectionBuilder()
                {
                    Server = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpServer],
                    Port = int.Parse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPort]),
                    Username = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUserName],
                    Password = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPassword],
                    UseSsl = bool.Parse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUseSsl])
                };

            @this.WithArkDefaultTargetsAndRules(
                logTableName, ConfigurationManager.ConnectionStrings[NLogDefaultConfigKeys.SqlConnStringName].ConnectionString,
                ConfigurationManager.AppSettings[NLogDefaultConfigKeys.MailNotificationAddresses] ?? mailTo, smtp.ConnectionString,
                mailFrom, async: async);

            var cfgSlack = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SlackWebHook];
            if (!string.IsNullOrWhiteSpace(cfgSlack))
                @this.WithSlackDefaultTargetsAndRules(cfgSlack, async);

            var iKey = ConfigurationManager.AppSettings["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? ConfigurationManager.AppSettings["ApplicationInsights:InstrumentationKey"];
            @this.WithApplicationInsightsTargetsAndRules(iKey, async);

            return @this;
        }
    }
}

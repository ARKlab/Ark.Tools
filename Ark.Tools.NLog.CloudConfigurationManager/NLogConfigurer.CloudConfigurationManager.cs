// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.NLog
{
    using Microsoft.Azure;
    using static Ark.Tools.NLog.NLogConfigurer;

    public static class NLogConfigurerCloudConfigurationManager
    {

        public static Configurer WithDefaultTargetsAndRulesFromCloudConfiguration(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true)
        {            
            var smtp = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpConnStringName)
                ?? new SmtpConnectionBuilder()
                {
                    Server = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpServer),
                    Port = int.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPort)),
                    Username = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUserName),
                    Password = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPassword),
                    UseSsl = bool.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUseSsl))
                }.ConnectionString;

            var cfg = new Config
            {
                SQLConnectionString = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SqlConnStringName),
                SQLTableName = logTableName,
                SmtpConnectionString = smtp,
                MailTo = mailTo ?? CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.MailNotificationAddresses),
                MailFrom = mailFrom,
                SlackWebhook = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SlackWebHook),
                ApplicationInsightsInstrumentationKey = CloudConfigurationManager.GetSetting("APPINSIGHTS_INSTRUMENTATIONKEY") 
                    ?? CloudConfigurationManager.GetSetting("ApplicationInsights:InstrumentationKey"),
                Async = async
            };

            @this.WithArkDefaultTargetsAndRules(cfg);

            return @this;
        }
    }
}

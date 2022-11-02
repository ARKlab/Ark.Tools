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
            var smtp = new SmtpConnectionBuilder(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpConnStringName))
                ?? new SmtpConnectionBuilder()
                {
                    Server = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpServer),
                    Port = int.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPort)),
                    Username = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUserName),
                    Password = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPassword),
                    UseSsl = bool.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUseSsl))
                };

            @this.WithArkDefaultTargetsAndRules(
                logTableName, CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SqlConnStringName),
                CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.MailNotificationAddresses) ?? mailTo, smtp.ConnectionString,
                mailFrom, async: async);

            var cfgSlack = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SlackWebHook);
            if (!string.IsNullOrWhiteSpace(cfgSlack))
                @this.WithSlackDefaultTargetsAndRules(cfgSlack, async);

            var iKey = CloudConfigurationManager.GetSetting("APPINSIGHTS_INSTRUMENTATIONKEY") ?? CloudConfigurationManager.GetSetting("ApplicationInsights:InstrumentationKey");
            @this.WithApplicationInsightsTargetsAndRules(iKey, async);

            return @this;
        }
    }
}

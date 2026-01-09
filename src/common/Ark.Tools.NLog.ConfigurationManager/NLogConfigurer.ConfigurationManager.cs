// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Configuration;
using System.Globalization;

using static Ark.Tools.NLog.NLogConfigurer;


namespace Ark.Tools.NLog;

public static class NLogConfigurerConfigurationManager
{

    public static Configurer WithDefaultTargetsAndRulesFromAppSettings(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true)
    {
        var smtp = ConfigurationManager.ConnectionStrings[NLogDefaultConfigKeys.SmtpConnStringName].ConnectionString
            ?? new SmtpConnectionBuilder()
            {
                Server = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpServer],
                Port = int.TryParse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPort], NumberStyles.None, CultureInfo.InvariantCulture, out var p) ? p : null,
                Username = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUserName],
                Password = ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPassword],
                UseSsl = bool.TryParse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUseSsl], out var b) ? b : true
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
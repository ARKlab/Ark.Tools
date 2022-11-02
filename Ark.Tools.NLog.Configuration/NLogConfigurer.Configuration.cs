// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.Configuration;

using System;

namespace Ark.Tools.NLog
{
    using static Ark.Tools.NLog.NLogConfigurer;

    public static class NLogConfigurerConfiguration
    {
        public static Configurer WithDefaultTargetsAndRulesFromConfiguration(this Configurer @this, string logTableName, string mailFrom, IConfiguration cfg, bool async = true)
        {
            @this.WithArkDefaultTargetsAndRules(
                logTableName, cfg.GetConnectionString(NLogDefaultConfigKeys.SqlConnStringName),
                cfg[NLogDefaultConfigKeys.MailNotificationAddresses.Replace('.', ':')], cfg.GetConnectionString(NLogDefaultConfigKeys.SmtpConnStringName),
                mailFrom, async:async);

            var cfgSlack = cfg[NLogDefaultConfigKeys.SlackWebHook.Replace('.', ':')];
            if (!string.IsNullOrWhiteSpace(cfgSlack))
                @this.WithSlackDefaultTargetsAndRules(cfgSlack, async);

            var key = cfg["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? cfg["ApplicationInsights:InstrumentationKey"];
            @this.WithApplicationInsightsTargetsAndRules(key, async);

            return @this;
        }
    }
}

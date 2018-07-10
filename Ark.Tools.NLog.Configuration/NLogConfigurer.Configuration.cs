// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.Configuration;

namespace Ark.Tools.NLog
{
    using static Ark.Tools.NLog.NLogConfigurer;

    public static class NLogConfigurerConfiguration
    {
        public static Configurer WithDefaultTargetsAndRulesFromConfiguration(this Configurer @this, string logTableName, string mailFrom, IConfiguration cfg, bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargetsAndRules(
                logTableName, cfg.GetConnectionString(NLogDefaultConfigKeys.SqlConnStringName), 
                mailFrom, cfg[NLogDefaultConfigKeys.MailNotificationAddresses.Replace('.',':')], 
                cfg.GetConnectionString(NLogDefaultConfigKeys.SmtpConnStringName), async);
            return @this;
        }
    }
}

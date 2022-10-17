// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.NLog
{
    using Microsoft.Azure;
    using static Ark.Tools.NLog.NLogConfigurer;

    public static class NLogConfigurerCloudConfigurationManager
    {
        public static Configurer WithMailTargetFromCloudConfiguration(this Configurer @this, string to, bool async = true)
        {
            return @this.WithMailTarget(
                  NLogConfigurer.MailFromDefault
                , to
                , CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpServer)
                , int.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPort))
                , CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUserName)
                , CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPassword)
                , bool.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUseSsl))
                , async
                );
        }

        public static Configurer WithMailTargetFromCloudConfiguration(this Configurer @this, string from, string to, bool async = true)
        {
            return @this.WithMailTarget(
                  from
                , to
                , CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpServer)
                , int.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPort))
                , CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUserName)
                , CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpPassword)
                , bool.Parse(CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SmtpUseSsl))
                , async
                );
        }

        public static Configurer WithSlackTargetFromCloudConfiguration(this Configurer @this, bool async = true)
        {
            var cfgSlack = CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SlackWebHook);
            if (!string.IsNullOrWhiteSpace(cfgSlack))
                @this.WithSlackDefaultTargetsAndRules(cfgSlack, async);

            return @this;
        }

        public static Configurer WithDatabaseTargetFromCloudConfiguration(this Configurer @this, string logTableName, bool async = true)
        {
            return @this.WithDatabaseTarget(logTableName, CloudConfigurationManager.GetSetting(NLogDefaultConfigKeys.SqlConnStringName), async: async);
        }

        public static Configurer WithDefaultTargetsFromCloudConfiguration(this Configurer @this, string logTableName, string mailTo, bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTargetFromCloudConfiguration(logTableName, async)
                        .WithMailTargetFromCloudConfiguration(mailTo, async)
                        .WithSlackTargetFromCloudConfiguration(async)
                        ;
        }
        public static Configurer WithDefaultTargetsFromCloudConfiguration(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTargetFromCloudConfiguration(logTableName, async)
                        .WithMailTargetFromCloudConfiguration(mailFrom, mailTo, async)
                        .WithSlackTargetFromCloudConfiguration(async)
                        ;
        }

        public static Configurer WithDefaultTargetsAndRulesFromCloudConfiguration(this Configurer @this, string logTableName, string mailTo, bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargetsFromCloudConfiguration(logTableName, mailTo, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();
            return @this;
        }
        public static Configurer WithDefaultTargetsAndRulesFromCloudConfiguration(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargetsFromCloudConfiguration(logTableName, mailFrom, mailTo, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();
            return @this;
        }
    }
}

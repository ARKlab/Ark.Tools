using System.Configuration;


namespace Ark.Tools.NLog
{
    using static Ark.Tools.NLog.NLogConfigurer;

    // TODO rename FromAppSettings to FromConfigurationManager
    // TODO add support for SmtpConnectionString
    public static class NLogConfigurerConfigurationManager
    {
        public static Configurer WithMailTargetFromAppSettings(this Configurer @this, string from, string to, bool async = true)
        {
            return @this.WithMailTarget(
                  from
                , to
                , ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpServer]
                , int.Parse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPort])
                , ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUserName]
                , ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpPassword]
                , bool.Parse(ConfigurationManager.AppSettings[NLogDefaultConfigKeys.SmtpUseSsl])
                );
        }

        public static Configurer WithDatabaseTargetFromAppSettings(this Configurer @this, string logTableName, bool async = true)
        {
            return @this.WithDatabaseTarget(logTableName, ConfigurationManager.ConnectionStrings[NLogDefaultConfigKeys.SqlConnStringName].ConnectionString, async: async);
        }

        public static Configurer WithDefaultTargetsFromAppSettings(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true)
        {
            return @this.WithConsoleTarget(async)
                        .WithFileTarget(async)
                        .WithDatabaseTargetFromAppSettings(logTableName, async)
                        .WithMailTargetFromAppSettings(mailFrom, mailTo, async: false)
                        ;
        }

        public static Configurer WithDefaultTargetsAndRulesFromAppSettings(this Configurer @this, string logTableName, string mailFrom, string mailTo, bool async = true, bool disableMailInDevelop = true)
        {
            @this.WithDefaultTargetsFromAppSettings(logTableName, mailFrom, mailTo, async);
            @this.WithDefaultRules();
            if (disableMailInDevelop)
                @this.DisableMailRuleWhenInVisualStudio();
            @this.ThrowInternalExceptionsInVisualStudio();
            return @this;
        }
    }
}

namespace Ark.Tools.NLog
{
    public abstract class NLogDefaultConfigKeys
    {
        public const string SqlConnStringName = "NLog.Database";
        public const string SmtpConnStringName = "NLog.Smtp";
        public const string MailNotificationAddresses = "NLog.NotificationList";

        public const string SmtpServer = "NLog.SmtpServer";
        public const string SmtpPort = "NLog.SmtpPort";
        public const string SmtpUserName = "NLog.SmtpUserName";
        public const string SmtpPassword = "NLog.SmtpPassword";
        public const string SmtpUseSsl = "NLog.SmtpUseSsl";
    };
}

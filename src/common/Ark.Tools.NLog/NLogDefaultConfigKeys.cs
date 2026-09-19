// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

namespace Ark.Tools.NLog;

public abstract class NLogDefaultConfigKeys
{
    [NotPersonalData("Configuration key name for the database logging connection string, not the underlying secret value.")]
    public const string SqlConnStringName = "NLog.Database";
    [NotPersonalData("Configuration key name for SMTP settings, not the underlying connection or credentials.")]
    public const string SmtpConnStringName = "NLog.Smtp";
    [NotPersonalData("Configuration key name for notification recipients, not the message data itself.")]
    public const string MailNotificationAddresses = "NLog.NotificationList";

    [NotPersonalData("Configuration key name for the SMTP host, not user data.")]
    public const string SmtpServer = "NLog.SmtpServer";
    [NotPersonalData("Configuration key name for the SMTP port, not user data.")]
    public const string SmtpPort = "NLog.SmtpPort";
    [NotPersonalData("Configuration key name for the SMTP username, not the username value.")]
    public const string SmtpUserName = "NLog.SmtpUserName";
    [NotPersonalData("Configuration key name for the SMTP password, not the secret value.")]
    public const string SmtpPassword = "NLog.SmtpPassword";
    [NotPersonalData("Configuration key name for the SMTP SSL flag, not a person or secret value.")]
    public const string SmtpUseSsl = "NLog.SmtpUseSsl";

    [NotPersonalData("Configuration key name for the Slack webhook, not the webhook URL value.")]
    public const string SlackWebHook = "NLog.SlackWebHook";
};
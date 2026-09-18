// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.NLog;

public abstract class NLogDefaultConfigKeys
{
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for the database logging connection string, not the underlying secret value.")]
    #endif
    public const string SqlConnStringName = "NLog.Database";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for SMTP settings, not the underlying connection or credentials.")]
    #endif
    public const string SmtpConnStringName = "NLog.Smtp";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for notification recipients, not the message data itself.")]
    #endif
    public const string MailNotificationAddresses = "NLog.NotificationList";

    #if NET10_0_OR_GREATER

    [NotPersonalData("Configuration key name for the SMTP host, not user data.")]

    #endif
    public const string SmtpServer = "NLog.SmtpServer";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for the SMTP port, not user data.")]
    #endif
    public const string SmtpPort = "NLog.SmtpPort";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for the SMTP username, not the username value.")]
    #endif
    public const string SmtpUserName = "NLog.SmtpUserName";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for the SMTP password, not the secret value.")]
    #endif
    public const string SmtpPassword = "NLog.SmtpPassword";
    #if NET10_0_OR_GREATER
    [NotPersonalData("Configuration key name for the SMTP SSL flag, not a person or secret value.")]
    #endif
    public const string SmtpUseSsl = "NLog.SmtpUseSsl";

    #if NET10_0_OR_GREATER

    [NotPersonalData("Configuration key name for the Slack webhook, not the webhook URL value.")]

    #endif
    public const string SlackWebHook = "NLog.SlackWebHook";
};
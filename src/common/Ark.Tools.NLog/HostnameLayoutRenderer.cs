// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog.Common;
using NLog.LayoutRenderers;

using System.Net;

namespace Ark.Tools.NLog;


[LayoutRenderer("ark.hostname")]
[AppDomainFixedOutput]
[ThreadAgnostic]
public class HostNameLayoutRenderer : LayoutRenderer
{
    internal string? _hostName { get; private set; }

    /// <summary>
    /// Initializes the layout renderer.
    /// </summary>
    protected override void InitializeLayoutRenderer()
    {
        base.InitializeLayoutRenderer();
        try
        {
            this._hostName = Environment.MachineName;
            try
            {

                var dns = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")
                    ?? Environment.GetEnvironmentVariable("RoleName")
                    ?? Dns.GetHostName()
                    ;

                if (dns is not null)
                    this._hostName = dns + "@" + this._hostName;

            }
#pragma warning disable ERP022 // Exit point swallows an unobserved exception - intentional
            catch { /* if we cannot get hostname - ignore */ }
#pragma warning restore ERP022
        }
        catch (Exception exception)
        {
            if (_mustBeRethrown(exception))
            {
                throw;
            }

            InternalLogger.Error("Error getting machine name {0}", exception);
            this._hostName = string.Empty;
        }
    }

    /// <summary>
    /// Renders the machine name and appends it to the specified <see cref="StringBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append the rendered data to.</param>
    /// <param name="logEvent">Logging event.</param>
    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
    {
        builder.Append(this._hostName);
    }

    private static bool _mustBeRethrown(Exception exception)
    {
        if (exception is StackOverflowException)
        {
            return true;
        }

        if (exception is ThreadAbortException)
        {
            return true;
        }

        if (exception is OutOfMemoryException)
        {
            return true;
        }

        if (exception is NLogConfigurationException)
        {
            return true;
        }

        if (exception.GetType().IsSubclassOf(typeof(NLogConfigurationException)))
        {
            return true;
        }

        return false;
    }
}
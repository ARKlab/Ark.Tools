// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.LayoutRenderers;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Ark.Tools.NLog
{

    [LayoutRenderer("ark.hostname")]
    [AppDomainFixedOutput]
    [ThreadAgnostic]
    public class HostNameLayoutRenderer : LayoutRenderer
    {
        internal string? HostName { get; private set; }

        /// <summary>
        /// Initializes the layout renderer.
        /// </summary>
        protected override void InitializeLayoutRenderer()
        {
            base.InitializeLayoutRenderer();
            try
            {
                this.HostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")
                    ?? Environment.GetEnvironmentVariable("RoleName")
                    ?? Environment.MachineName;
            }
            catch (Exception exception)
            {
                if (MustBeRethrown(exception))
                {
                    throw;
                }

                InternalLogger.Error("Error getting machine name {0}", exception);
                this.HostName = string.Empty;
            }
        }

        /// <summary>
        /// Renders the machine name and appends it to the specified <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder"/> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(this.HostName);
        }

        private static bool MustBeRethrown(Exception exception)
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
}

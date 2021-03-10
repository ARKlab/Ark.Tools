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
    [LayoutRenderer("ark.activityid")]
    [ThreadSafe]
    public class ActivityIdLayoutRenderer : LayoutRenderer
    {
        /// <summary>
        /// Renders the machine name and appends it to the specified <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder"/> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            var id = Activity.Current?.Id;
            if (id != null)
                builder.Append(id);
        }
    }
}

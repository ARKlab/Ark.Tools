// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog.LayoutRenderers;

using System.Diagnostics;

namespace Ark.Tools.NLog;

/// <summary>
/// Improved stack traces when logging exceptions via @benaadams' https://github.com/benaadams/Ben.Demystifier.
/// Replace ${exception} in your targets with a demystified version.
/// </summary>
[LayoutRenderer("exception")]
[ThreadAgnostic]
public class DemystifiedExceptionLayoutRenderer : ExceptionLayoutRenderer
{
    private static readonly bool _demystifierEnabled =
        !string.Equals(
            Environment.GetEnvironmentVariable("ARK_TOOLS_DISABLE_DEMYSTIFIER"),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// Appends the stack trace from an Exception to the specified <see cref="StringBuilder" />.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append the rendered data to.</param>
    /// <param name="ex">The Exception whose stack trace should be appended.</param>
    protected override void AppendStackTrace(StringBuilder sb, Exception ex)
    {
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            if (_demystifierEnabled)
                sb.Append(ex.Demystify().StackTrace);
            else
                base.AppendStackTrace(sb, ex);
        }
    }

    /// <summary>
    /// Appends the result of calling ToString() on an Exception to the specified <see cref="StringBuilder" />.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append the rendered data to.</param>
    /// <param name="ex">The Exception whose call to ToString() should be appended.</param>
    protected override void AppendToString(StringBuilder sb, Exception ex)
    {
        if (_demystifierEnabled)
            sb.Append(ex.Demystify().ToString());
        else
            base.AppendToString(sb, ex);
    }
}
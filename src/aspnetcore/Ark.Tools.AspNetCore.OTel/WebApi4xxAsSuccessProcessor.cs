// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// Marks ASP.NET Core server spans for HTTP 4xx responses as non-errors.
/// </summary>
public sealed class WebApi4xxAsSuccessProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Kind != ActivityKind.Server)
            return;

        var statusCode = data.GetTagItem("http.response.status_code") switch
        {
            int value => value,
            long value => (int)value,
            string value when int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };

        if (statusCode is >= 400 and < 500)
            data.SetStatus(ActivityStatusCode.Unset);
    }
}

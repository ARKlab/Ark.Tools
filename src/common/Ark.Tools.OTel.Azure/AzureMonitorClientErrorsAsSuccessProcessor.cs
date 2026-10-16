// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.OTel.Azure;

/// <summary>
/// Sets <see cref="ActivityStatusCode.Ok"/> on server spans that end with an HTTP 4xx code and an
/// <see cref="ActivityStatusCode.Unset"/> status, so Azure Monitor shows them as successful requests.
/// </summary>
/// <remarks>
/// The Azure Monitor exporter treats an <see cref="ActivityStatusCode.Unset"/> request as successful only below 400.
/// An <see cref="ActivityStatusCode.Error"/> or <see cref="ActivityStatusCode.Ok"/> set by the producer is kept.
/// </remarks>
internal sealed class AzureMonitorClientErrorsAsSuccessProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (data.Kind != ActivityKind.Server || data.Status != ActivityStatusCode.Unset)
            return;

        var statusCode = data.GetTagItem("http.response.status_code") switch
        {
            int value => value,
            long value => value,
            string value when int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0L
        };

        if (statusCode is >= 400 and < 500)
            data.SetStatus(ActivityStatusCode.Ok);
    }
}

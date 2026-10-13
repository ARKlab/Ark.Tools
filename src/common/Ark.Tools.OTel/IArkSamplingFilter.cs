// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;

namespace Ark.Tools.OTel;

/// <summary>
/// Recognizes filtered spans from their name and creation tags, so <see cref="ArkAdaptiveSampler"/> drops them
/// before sampling and they do not use its budget.
/// </summary>
/// <remarks>
/// Register implementations in the service collection before the tracer provider is built:
/// the sampler of <see cref="ArkOpenTelemetryBuilderExtensions.AddArkOpenTelemetry"/> applies them all.
/// </remarks>
public interface IArkSamplingFilter
{
    /// <summary>
    /// Returns <see langword="true"/> to drop the span described by <paramref name="samplingParameters"/>.
    /// </summary>
    /// <param name="samplingParameters">The name, kind, parent and creation tags of the span.</param>
    /// <returns><see langword="true"/> to drop the span.</returns>
    bool ShouldDrop(in SamplingParameters samplingParameters);
}

// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.OTel;

/// <summary>
/// Configuration for Ark OpenTelemetry instrumentation.
/// </summary>
public sealed class ArkOtelConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether SQL query text is retained on exported spans.
    /// </summary>
    public bool IncludeSqlQueryText { get; set; }

    /// <summary>
    /// Gets or sets labels for SQL commands that should not produce spans.
    /// The <c>outbox.peek-lock</c> label is skipped by default.
    /// </summary>
    public IEnumerable<string>? SqlQueryLabelsToSkip { get; set; }
}

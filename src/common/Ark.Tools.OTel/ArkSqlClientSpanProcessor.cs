// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// Redacts SQL query text and extracts labels from SQL linting comments.
/// </summary>
public sealed class ArkSqlClientSpanProcessor : BaseProcessor<Activity>
{
    private readonly bool _includeQueryText;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArkSqlClientSpanProcessor"/> class.
    /// </summary>
    /// <param name="includeQueryText">
    /// Whether to retain <c>db.query.text</c>. The default application setup omits it.
    /// </param>
    public ArkSqlClientSpanProcessor(bool includeQueryText = false)
    {
        _includeQueryText = includeQueryText;
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (data.GetTagItem("db.query.text") is not string queryText)
            return;

        ArkSqlQueryLabel.SetTag(data, queryText);

        var dbSystem = data.GetTagItem("db.system.name") as string
                     ?? data.GetTagItem("db.system") as string;
        if (dbSystem is not null
            && !dbSystem.Equals("mssql", StringComparison.OrdinalIgnoreCase)
            && !dbSystem.Equals("microsoft.sql", StringComparison.OrdinalIgnoreCase)
            && !dbSystem.Equals("microsoft.sql_server", StringComparison.OrdinalIgnoreCase))
            return;

        if (!_includeQueryText)
            data.SetTag("db.query.text", null);
    }
}

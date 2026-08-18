// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ark.Tools.OTel;

/// <summary>
/// Redacts SQL query text and improves summaries on SQL client spans.
/// </summary>
public sealed partial class ArkSqlClientSpanProcessor : BaseProcessor<Activity>
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

        var dbSystem = data.GetTagItem("db.system.name") as string
                     ?? data.GetTagItem("db.system") as string;
        if (dbSystem is not null
            && !dbSystem.Equals("mssql", StringComparison.OrdinalIgnoreCase)
            && !dbSystem.Equals("microsoft.sql", StringComparison.OrdinalIgnoreCase)
            && !dbSystem.Equals("microsoft.sql_server", StringComparison.OrdinalIgnoreCase))
            return;

        var summary = _createSummary(queryText);
        if (summary is not null)
            data.SetTag("db.query.summary", summary);

        if (!_includeQueryText)
            data.SetTag("db.query.text", null);
    }

    private static string? _createSummary(string queryText)
    {
        var normalized = _whitespace().Replace(queryText, " ").Trim();
        if (normalized.Length == 0)
            return null;

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2 || !tokens[0].Equals("INSERT", StringComparison.OrdinalIgnoreCase))
            return null;

        var intoIndex = Array.FindIndex(
            tokens,
            token => token.Equals("INTO", StringComparison.OrdinalIgnoreCase));
        if (intoIndex < 0 || intoIndex == tokens.Length - 1)
            return null;

        var table = tokens[intoIndex + 1].Trim('[', ']', ',', ';');
        return table.Length == 0 ? null : "INSERT INTO " + table;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex _whitespace();
}

// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// Extracts labels from SQL linting comments.
/// </summary>
public static class ArkSqlQueryLabel
{
    /// <summary>
    /// The activity tag used for an extracted query label.
    /// </summary>
    public const string TagName = "db.query.label";

    private const string _commentMarker = "--";
    private const string _labelPrefix = "otel-query-label:";
    private const int _maxLabelLength = 255;

    /// <summary>
    /// Extracts and sanitizes the first <c>otel-query-label</c> comment in the supplied SQL text.
    /// </summary>
    /// <param name="queryText">The SQL command text.</param>
    /// <returns>The sanitized label, or <see langword="null"/> when no valid label exists.</returns>
    public static string? Extract(string? queryText)
    {
        if (string.IsNullOrEmpty(queryText))
            return null;

        var lineStart = 0;
        while (lineStart < queryText.Length)
        {
            var lineEnd = queryText.IndexOfAny(['\r', '\n'], lineStart);
            if (lineEnd < 0)
                lineEnd = queryText.Length;

            var line = queryText.AsSpan(lineStart, lineEnd - lineStart);
            var commentStart = _findCommentStart(line);
            if (commentStart >= 0)
            {
                var value = line[(commentStart + _commentMarker.Length)..].TrimStart();
                if (value.StartsWith(_labelPrefix, StringComparison.OrdinalIgnoreCase))
                    return _sanitize(value[_labelPrefix.Length..]);
            }

            lineStart = lineEnd;
            while (lineStart < queryText.Length && queryText[lineStart] is '\r' or '\n')
                lineStart++;
        }

        return null;
    }

    private static int _findCommentStart(ReadOnlySpan<char> line)
    {
        var inStringLiteral = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '\'')
            {
                if (inStringLiteral && index + 1 < line.Length && line[index + 1] == '\'')
                {
                    index++;
                    continue;
                }

                inStringLiteral = !inStringLiteral;
                continue;
            }

            if (!inStringLiteral
                && line[index] == '-'
                && index + 1 < line.Length
                && line[index + 1] == '-')
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Sets the extracted query label on an activity when the SQL text contains one.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="queryText">The SQL command text.</param>
    public static void SetTag(Activity activity, string? queryText)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var label = Extract(queryText);
        if (label is not null)
            activity.SetTag(TagName, label);
    }

    private static string? _sanitize(ReadOnlySpan<char> value)
    {
        var builder = new StringBuilder(Math.Min(value.Length, _maxLabelLength));
        var pendingSpace = false;

        foreach (var character in value)
        {
            if (char.IsControl(character))
                continue;

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (builder.Length + (pendingSpace ? 2 : 1) > _maxLabelLength)
                break;

            if (pendingSpace)
                builder.Append(' ');

            builder.Append(character);
            pendingSpace = false;
        }

        var sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? null : sanitized;
    }
}

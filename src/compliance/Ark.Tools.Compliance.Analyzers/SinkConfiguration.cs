// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Analyzers;

internal sealed class SinkConfiguration
{
    private readonly ImmutableArray<Entry> _entries;

    private SinkConfiguration(ImmutableArray<Entry> entries)
    {
        _entries = entries;
    }

    internal static SinkConfiguration _read(AnalyzerOptions options, CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<Entry>();
        foreach (var text in options.AdditionalFiles
            .Where(static file => Path.GetFileName(file.Path).StartsWith("ComplianceSinks", StringComparison.OrdinalIgnoreCase)
                && file.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static file => string.Equals(Path.GetFileName(file.Path), "ComplianceSinks.Ark.txt", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static file => file.Path, StringComparer.Ordinal)
            .Select(file => file.GetText(cancellationToken)))
        {
            if (text is null)
            {
                continue;
            }

            foreach (var line in text.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = line.ToString().Trim();
                if (value.Length == 0 || value.StartsWith("#", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = value.Split(';');
                var pattern = parts[0].Trim();
                var remove = pattern.StartsWith("-", StringComparison.Ordinal);
                if (remove || pattern.StartsWith("+", StringComparison.Ordinal))
                {
                    pattern = pattern.Substring(1).Trim();
                }

                var rule = parts.Length > 1 ? _rule(parts[1].Trim()) : null;
                if (pattern.StartsWith("M:", StringComparison.Ordinal) && (remove || rule is not null))
                {
                    entries.Add(new Entry(pattern, remove ? null : rule));
                }
            }
        }

        return new SinkConfiguration(entries.ToImmutable());
    }

    internal string? _getRule(IMethodSymbol method)
    {
        method = (method.ReducedFrom ?? method).OriginalDefinition;
        var id = method.GetDocumentationCommentId();
        for (var index = _entries.Length - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            if (id is not null && entry._matches(id))
            {
                return entry._rule;
            }
        }

        var type = method.ContainingType;
        var name = method.Name;
        if (method.MethodKind == MethodKind.Constructor && _isOrDerivesFrom(type, "System.Exception"))
        {
            return "ARKPII003";
        }

        if ((_isOrDerivesFrom(type, "NLog.Logger") || _implements(type, "NLog.ILogger"))
            && name is "Trace" or "Debug" or "Info" or "Warn" or "Error" or "Fatal" or "Log" or "BeginScope")
        {
            return "ARKPII002";
        }

        if ((_implements(type, "Microsoft.Extensions.Logging.ILogger")
                || type.ToDisplayString() == "Microsoft.Extensions.Logging.LoggerExtensions")
            && (name.StartsWith("Log", StringComparison.Ordinal) || name == "BeginScope"))
        {
            return "ARKPII002";
        }

        if (_isOrDerivesFrom(type, "System.Diagnostics.Activity")
            && name is "SetTag" or "AddTag" or "SetBaggage" or "AddBaggage" or "AddEvent")
        {
            return "ARKPII004";
        }

        if (type.ContainingNamespace.ToDisplayString() == "System.Diagnostics.Metrics"
            && name is "Add" or "Record")
        {
            return "ARKPII004";
        }

        if (type.ToDisplayString() is "System.Console" or "System.Diagnostics.Debug" or "System.Diagnostics.Trace"
            || (type.ToDisplayString() == "System.Text.StringBuilder" && name.StartsWith("Append", StringComparison.Ordinal)))
        {
            return "ARKPII011";
        }

        return null;
    }

    internal static int _getLimit(AnalyzerConfigOptions options, string key, int defaultValue, int maximum)
    {
        return options.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0
            ? Math.Min(parsed, maximum)
            : defaultValue;
    }

    internal static bool _isOrDerivesFrom(INamedTypeSymbol? type, string metadataName)
    {
        for (var depth = 0; type is not null && depth < 64; depth++, type = type.BaseType)
        {
            if (type.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool _implements(INamedTypeSymbol type, string metadataName)
    {
        return _isOrDerivesFrom(type, metadataName)
            || type.AllInterfaces.Any(item => item.ToDisplayString() == metadataName);
    }

    private static string? _rule(string kind)
    {
        return kind switch
        {
            "log" or "ARKPII002" => "ARKPII002",
            "exception" or "ARKPII003" => "ARKPII003",
            "telemetry" or "ARKPII004" => "ARKPII004",
            "format" or "ARKPII011" => "ARKPII011",
            _ => null,
        };
    }

    private sealed class Entry
    {
        internal Entry(string pattern, string? rule)
        {
            _pattern = pattern;
            _rule = rule;
        }

        private string _pattern { get; }

        internal string? _rule { get; }

        internal bool _matches(string id)
        {
            return _pattern.EndsWith("*", StringComparison.Ordinal)
                ? id.StartsWith(_pattern.Substring(0, _pattern.Length - 1), StringComparison.Ordinal)
                : string.Equals(id, _pattern, StringComparison.Ordinal);
        }
    }
}
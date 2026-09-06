// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Analyzers;

internal sealed class ComplianceLexicon
{
    private static readonly ConditionalWeakTable<SourceText, ParsedEntries> _snapshots = new();
    private static readonly char[] _newLines = { '\r', '\n' };
    private readonly HashSet<string> _terms = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);

    internal ComplianceLexicon(ImmutableArray<AdditionalText> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.GetFileName(file.Path).StartsWith("ComplianceLexicon", StringComparison.OrdinalIgnoreCase)
                || !file.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is not null)
            {
                _add(_snapshots.GetValue(text, static source => new ParsedEntries(source.ToString())));
            }
        }
    }

    internal bool _matches(string name)
    {
        name = name.TrimStart('_');
        foreach (var excluded in _excluded)
        {
            if (_matches(name, excluded))
            {
                return false;
            }
        }

        foreach (var term in _terms)
        {
            if (_matches(name, term))
            {
                return true;
            }
        }

        return false;
    }

    private static bool _matches(string name, string term)
    {
        if (term.EndsWith("*", StringComparison.Ordinal))
        {
            return name.StartsWith(term.Substring(0, term.Length - 1), StringComparison.OrdinalIgnoreCase);
        }

        for (var index = 0; index + term.Length <= name.Length; index++)
        {
            if (_isBoundary(name, index) && _isBoundary(name, index + term.Length)
                && string.Compare(name, index, term, 0, term.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool _isBoundary(string name, int index)
    {
        return index == 0 || index == name.Length || name[index - 1] == '_' || name[index] == '_'
            || (char.IsUpper(name[index]) && (!char.IsUpper(name[index - 1])
                || (index + 1 < name.Length && char.IsLower(name[index + 1]))));
    }

    private void _add(ParsedEntries entries)
    {
        foreach (var entry in entries._entries)
        {
            if (entry[0] == '-')
            {
                _excluded.Add(entry.Substring(1));
            }
            else
            {
                _terms.Add(entry.TrimStart('+'));
            }
        }
    }

    private sealed class ParsedEntries
    {
        internal ParsedEntries(string text)
        {
            var entries = ImmutableArray.CreateBuilder<string>();
            foreach (var line in text.Split(_newLines, StringSplitOptions.RemoveEmptyEntries))
            {
                var comment = line.IndexOf('#');
                var entry = (comment >= 0 ? line.Substring(0, comment) : line).Trim();
                if (entry.Length > 0 && entry is not "-" and not "+" and not "*")
                {
                    entries.Add(entry);
                }
            }

            _entries = entries.ToImmutable();
        }

        internal ImmutableArray<string> _entries { get; }
    }
}

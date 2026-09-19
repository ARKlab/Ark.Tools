// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.Compliance.Generators;

public sealed partial class ComplianceSurfaceGenerator
{
    private static SurfaceSpec _build(
        ImmutableEquatableArray<TypeSpec> types,
        ImmutableEquatableArray<RevealSpec> reveals,
        ImmutableEquatableArray<RegistrationSpec> registrationSpecs,
        CancellationToken token)
    {
        var notes = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var reveal in reveals.Items)
        {
            token.ThrowIfCancellationRequested();
            _add(notes, reveal.MemberKey, reveal.Note);
        }

        var registrations = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var registration in registrationSpecs.Items)
        {
            token.ThrowIfCancellationRequested();
            foreach (var typeName in registration.TypeNames.Items)
            {
                _add(registrations, typeName, registration.Serializer);
            }
        }

        var entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types.Items)
        {
            token.ThrowIfCancellationRequested();
            foreach (var member in type.Members.Items)
            {
                token.ThrowIfCancellationRequested();
                members.Add(member.Key);
                if (member.Classifications.Items.Length == 0)
                    continue;

                var purposeNotes = new SortedSet<string>(StringComparer.Ordinal);
                if (member.Documentation is not null)
                    purposeNotes.Add(member.Documentation);
                if (notes.TryGetValue(member.Key, out var purposes))
                    purposeNotes.UnionWith(purposes);
                var serializers = _serializers(member, registrations);
                var fields = new[]
                {
                    "CLASSIFIED", type.Name, member.Name, string.Join(",", member.Classifications.Items),
                    string.Join("; ", purposeNotes), string.Join(",", serializers),
                };
                entries[member.Key] = new Entry(
                    member.Key,
                    string.Join("\t", fields.Select(_escape)),
                    member.Classifications,
                    member.Location);
            }
        }

        return new SurfaceSpec(
            Header + "\n" + string.Join(string.Empty, entries.Values
                .OrderBy(static entry => entry.Line, StringComparer.Ordinal)
                .Select(static entry => entry.Line + "\n")),
            new ImmutableEquatableArray<Entry>(entries.Values.ToImmutableArray()),
            new ImmutableEquatableArray<string>(
                members.OrderBy(static member => member, StringComparer.Ordinal).ToImmutableArray()));
    }

    private static SortedSet<string> _serializers(
        MemberSpec member,
        Dictionary<string, SortedSet<string>> registrations)
    {
        var result = new SortedSet<string>(StringComparer.Ordinal);
        if (!member.IsSerializable)
            return result;

        foreach (var typeName in member.ValueTypeNames.Items)
        {
            if (registrations.TryGetValue(typeName, out var serializers))
                result.UnionWith(serializers);
        }
        if (member.HasSensitiveValueObject)
            result.Add("System.Text.Json");
        result.UnionWith(member.SerializerAttributes.Items);
        result.ExceptWith(member.IgnoredSerializers.Items);
        return result;
    }

    private static void _add(Dictionary<string, SortedSet<string>> result, string key, string value)
    {
        if (!result.TryGetValue(key, out var values))
            result.Add(key, values = new SortedSet<string>(StringComparer.Ordinal));
        values.Add(value);
    }

    private static string _escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r")
            .Replace("\n", "\\n").Replace("*/", "*\\/").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
    }

    private static void _verify(
        SourceProductionContext context,
        SurfaceSpec surface,
        ImmutableEquatableArray<string?> files)
    {
        if (files.Items.Length == 0 && surface.Entries.Items.Length == 0)
            return;
        if (files.Items.Length != 1 || files.Items[0] is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_drift, Location.None,
                files.Items.Length > 1 ? "multiple baselines supplied" : "baseline missing or unreadable"));
            return;
        }
        if (!_parse(files.Items[0]!, out var baseline))
        {
            context.ReportDiagnostic(Diagnostic.Create(_drift, Location.None, "baseline is malformed"));
            return;
        }
        foreach (var item in surface.Entries.Items
            .Select(entry =>
            {
                var found = baseline.TryGetValue(entry.Key, out var previous);
                return (entry, found, previous);
            })
            .Where(static item => !(item.found && item.previous is not null && item.previous.Line == item.entry.Line)))
        {
            var entry = item.entry;
            var previous = item.previous;
            context.ReportDiagnostic(Diagnostic.Create(_drift, entry.Location.ToLocation(), entry.Key.Replace('\t', '.')));
            if (previous is null || previous.Classifications.Items.Any(
                    old => !entry.Classifications.Items.Any(current => _covers(current, old))))
                context.ReportDiagnostic(Diagnostic.Create(_weakened, entry.Location.ToLocation(), entry.Key.Replace('\t', '.')));
        }
        var currentKeys = new HashSet<string>(
            surface.Entries.Items.Select(static entry => entry.Key),
            StringComparer.Ordinal);
        foreach (var previous in baseline.Values.Where(previous => !currentKeys.Contains(previous.Key)))
        {
            context.ReportDiagnostic(Diagnostic.Create(_drift, Location.None, previous.Key.Replace('\t', '.')));
            if (surface.Members.Items.Contains(previous.Key, StringComparer.Ordinal))
                context.ReportDiagnostic(Diagnostic.Create(_weakened, Location.None, previous.Key.Replace('\t', '.')));
        }
    }

    private static bool _covers(string current, string previous)
    {
        return current == previous
            || (previous == "Ark:Pseudonymous" && current is "Ark:PersonalData" or "Ark:SensitivePersonalData")
            || (previous == "Ark:PersonalData" && current == "Ark:SensitivePersonalData");
    }

    private static bool _parse(string text, out Dictionary<string, Entry> entries)
    {
        entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        text = text.Replace("\r\n", "\n").TrimStart('\uFEFF').TrimEnd('\n');
        if (text.StartsWith("/*\n", StringComparison.Ordinal) && text.EndsWith("\n*/", StringComparison.Ordinal))
            text = text.Substring(3, text.Length - 6).TrimEnd('\n');
        var lines = text.Split('\n');
        if (lines.Length == 0 || lines[0].TrimStart('\uFEFF') != Header)
            return false;
        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split('\t');
            if (fields.Length != 6 || fields[0] != "CLASSIFIED" || fields[1].Length == 0 || fields[2].Length == 0)
                return false;
            var classifications = fields[3].Split(',');
            if (classifications.Any(static classification => classification is not
                    ("Ark:PersonalData" or "Ark:SensitivePersonalData" or "Ark:UserCredentials" or "Ark:InfrastructureSecret" or "Ark:Pseudonymous")))
                return false;
            var key = fields[1] + "\t" + fields[2];
            if (entries.ContainsKey(key))
                return false;
            entries.Add(
                key,
                new Entry(
                    key,
                    line,
                    new ImmutableEquatableArray<string>(classifications.ToImmutableArray()),
                    default));
        }
        return true;
    }

    private sealed record Entry(
        string Key,
        string Line,
        ImmutableEquatableArray<string> Classifications,
        LocationSpec Location);

    private sealed record SurfaceSpec(
        string Text,
        ImmutableEquatableArray<Entry> Entries,
        ImmutableEquatableArray<string> Members);
}

// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance.Shared;

using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ark.Tools.Compliance;

/// <summary>Redacts classified values and projects object graphs into safe sink values.</summary>
/// <remarks>
/// Generated sensitive values use a reflection-free contract. Dynamic objects are inspected
/// with bounded depth and collection size; missing metadata, getter failures and cycles erase.
/// Configure this immutable policy once, rather than once per event.
/// </remarks>
public sealed class ComplianceRedactor
{
    /// <summary>The alertable marker emitted by runtime erasure and pattern scanning.</summary>
    public const string Marker = "***ARKPII***";

    private const int _maxDepth = 8;
    private const int _maxItems = 64;
    private readonly Dictionary<DataClassification, ArkRedaction> _classifications;
    private readonly ArkRedaction _default;
    private readonly ArkHmacRedactor _hmac;
    private readonly ConcurrentDictionary<Type, TypeMetadata> _metadata = new();

    /// <summary>Initializes an immutable redaction policy.</summary>
    /// <param name="options">Overrides, or null for fail-closed defaults.</param>
    public ComplianceRedactor(ComplianceRedactionOptions? options = null)
    {
        options ??= new();
        _classifications = options._snapshot();
        _default = options.Default;
        _hmac = new ArkHmacRedactor(options.HmacKey);
        PatternScan = options.PatternScan;
    }

    /// <summary>Gets the configured pattern scanning mode.</summary>
    public PatternScanMode PatternScan { get; }

    /// <summary>Redacts a value without changing the original object graph.</summary>
    /// <param name="value">A scalar, classified value, or structured payload.</param>
    /// <returns>A sink-safe value or projection.</returns>
    public object? Redact(object? value)
    {
        var remaining = 256;
        return _redact(value, 0, ref remaining);
    }

    /// <summary>Applies the selected policy to explicitly classified text.</summary>
    /// <param name="value">The cleartext input.</param>
    /// <param name="classification">The input classification.</param>
    /// <returns>The redacted text.</returns>
    public string Redact(string value, DataClassification classification)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _apply(value, _getRedaction(classification));
    }

    /// <summary>Scans untyped text only when pattern scanning is enabled.</summary>
    /// <param name="value">The text to inspect.</param>
    /// <returns>The original text when scanning is disabled, otherwise the masked text.</returns>
    public string Scan(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return PatternScan == PatternScanMode.Off ? value : PiiPatternScanner._redact(value, Marker);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed redaction must never fall back to cleartext.")]
    [SuppressMessage("Correctness", "ERP022", Justification = "Fail-closed boundary intentionally discards potentially confidential exception messages.")]
    private object? _redact(object? value, int depth, ref int remaining)
    {
        try
        {
            if (value is null)
                return null;
            if (depth >= _maxDepth || --remaining < 0)
                return Marker;
            if (value is IRuntimeClassifiedValue sensitive)
            {
                var redaction = _getRedaction(sensitive.Classification);
                if (redaction is ArkRedaction.Erase or ArkRedaction.Mask)
                    return Marker;
                return _normalizeMarker(sensitive.Redact(_getRedactor(redaction)));
            }
            if (value is string text)
                return Scan(text);

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid)
                return value;
            if (!RuntimeFeature.IsDynamicCodeSupported)
                return Marker;

            var metadata = _metadata.GetOrAdd(type, static type => _getMetadata(type));
            if (metadata.Classifications.Length != 0)
                return _redactClassified(value, metadata.Classifications);

            if (value is IDictionary dictionary)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (result.Count >= _maxItems || remaining <= 0)
                        return Marker;
                    var key = entry.Key is string name ? Scan(name) : Marker;
                    result[key] = _redact(entry.Value, depth + 1, ref remaining);
                }
                return result;
            }
            if (value is IEnumerable sequence)
            {
                var result = new List<object?>();
                foreach (var item in sequence)
                {
                    if (result.Count >= _maxItems || remaining <= 0)
                        return Marker;
                    result.Add(_redact(item, depth + 1, ref remaining));
                }
                return result;
            }
            if (metadata.Members.Length == 0 || metadata.Members.Length > _maxItems)
                return Marker;

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var member in metadata.Members)
            {
                if (remaining <= 0)
                    return Marker;
                var classifications = member.GetCustomAttributes<DataClassificationAttribute>(inherit: true)
                    .Select(static attribute => attribute.Classification).ToArray();
                if (classifications.Length > 0 && _selectRedaction(classifications) is ArkRedaction.Erase or ArkRedaction.Mask)
                {
                    properties[member.Name] = Marker;
                    continue;
                }

                var memberValue = member is PropertyInfo property ? property.GetValue(value) : ((FieldInfo)member).GetValue(value);
                properties[member.Name] = classifications.Length > 0 && memberValue is not null
                    ? _redactClassified(memberValue, classifications)
                    : _redact(memberValue, depth + 1, ref remaining);
            }
            return properties;
        }
        catch
        {
            return Marker;
        }
    }

    private string _redactClassified(object value, DataClassification[] classifications)
    {
        var redaction = _selectRedaction(classifications);
        if (redaction is ArkRedaction.Erase or ArkRedaction.Mask)
            return Marker;
        var text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        return _apply(text ?? string.Empty, redaction);
    }

    private ArkRedaction _selectRedaction(DataClassification[] classifications)
    {
        var result = ArkRedaction.None;
        foreach (var classification in classifications)
        {
            var current = _getRedaction(classification);
            if (current is ArkRedaction.Erase or ArkRedaction.Mask)
                return current;
            if (current == ArkRedaction.Hmac)
                result = current;
        }
        return result;
    }

    private ArkRedaction _getRedaction(DataClassification classification)
    {
        var redaction = _classifications.TryGetValue(classification, out var configured) ? configured : _default;
        return redaction is ArkRedaction.Erase or ArkRedaction.Mask or ArkRedaction.Hmac or ArkRedaction.None
            ? redaction
            : ArkRedaction.Erase;
    }

    private string _apply(string value, ArkRedaction redaction)
    {
        return redaction is ArkRedaction.Erase or ArkRedaction.Mask
            ? Marker
            : _normalizeMarker(_getRedactor(redaction).Redact(value));
    }

    private Redactor _getRedactor(ArkRedaction redaction)
    {
        return redaction switch
        {
            ArkRedaction.Hmac => _hmac,
            ArkRedaction.None => ArkNullRedactor.Instance,
            _ => ArkErasingRedactor.Instance,
        };
    }

    private static string _normalizeMarker(string value)
    {
        return value == ArkErasingRedactor.Marker ? Marker : value;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Generated types use IRuntimeClassifiedValue. Missing dynamic metadata erases instead of rendering an opaque object.")]
    private static TypeMetadata _getMetadata(Type type)
    {
        return new(
            type.GetCustomAttributes<DataClassificationAttribute>(inherit: true).Select(static attribute => attribute.Classification).ToArray(),
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Cast<MemberInfo>()
                .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                .ToArray());
    }

    private sealed record TypeMetadata(DataClassification[] Classifications, MemberInfo[] Members);
}

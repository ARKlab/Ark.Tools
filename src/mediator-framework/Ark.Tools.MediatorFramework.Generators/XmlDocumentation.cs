// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Xml.Linq;
using System.Threading;
using System;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>Reads contract documentation the same way for every transport generator.</summary>
internal static class XmlDocumentation
{
    /// <summary>
    /// Returns the summary and remarks of <paramref name="symbol"/>, parsing its XML documentation at most once.
    /// A <c>[Description]</c> attribute wins over the XML summary and suppresses the remarks.
    /// </summary>
    public static (string? Summary, string? Remarks) Read(ISymbol symbol, CancellationToken cancellationToken)
    {
        if (_tryGetDescription(symbol, out var description))
            return (description, null);

        var root = _parse(symbol, cancellationToken);
        return (_text(root?.Element("summary")), _text(root?.Element("remarks")));
    }

    /// <summary>Returns the summary of <paramref name="symbol"/>, preferring a <c>[Description]</c> attribute.</summary>
    public static string? Summary(ISymbol symbol, CancellationToken cancellationToken)
    {
        return _tryGetDescription(symbol, out var description)
            ? description
            : _text(_parse(symbol, cancellationToken)?.Element("summary"));
    }

    private static bool _tryGetDescription(ISymbol symbol, out string? description)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(static candidate =>
            candidate.AttributeClass._isFullName("System.ComponentModel.DescriptionAttribute"));
        description = attribute?.ConstructorArguments.FirstOrDefault().Value as string;
        if (description is not null)
            description = string.Join(' ', description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return attribute is not null;
    }

    private static XElement? _parse(ISymbol symbol, CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            return XDocument.Parse(xml).Root;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? _text(XElement? element)
    {
        return element is null ? null : _normalize(element);
    }

    private static string? _normalize(XElement element)
    {
        var text = string.Concat(element.Nodes().Select(static node => node switch
        {
            XText value => value.Value,
            XElement child when child.Name.LocalName == "see"
                => child.Attribute("cref")?.Value.TrimStart("!:".ToCharArray()).Split('.').LastOrDefault() ?? string.Empty,
            // Line breaks and paragraphs separate words even when written without surrounding whitespace.
            XElement child when child.Name.LocalName is "br" or "para"
                => " " + _normalize(child) + " ",
            XElement child => _normalize(child) ?? string.Empty,
            _ => string.Empty,
        }));
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

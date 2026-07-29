// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Xml.Linq;
using System.Threading;
using System;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.MediatorFramework.Generators;

internal static class XmlDocumentation
{
    public static string? Summary(ISymbol symbol, CancellationToken cancellationToken = default)
        => Read(symbol, "summary", cancellationToken);

    public static string? Remarks(ISymbol symbol, CancellationToken cancellationToken = default)
        => Read(symbol, "remarks", cancellationToken);

    private static string? Read(ISymbol symbol, string elementName, CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var element = XDocument.Parse(xml).Root?.Element(elementName);
            return element is null ? null : Normalize(element).NullIfEmpty();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string Normalize(XElement element)
    {
        var text = string.Concat(element.Nodes().Select(node => node switch
        {
            XText value => value.Value,
            XElement child when child.Name.LocalName == "see"
                => child.Attribute("cref")?.Value.TrimStart("!:".ToCharArray()).Split('.').LastOrDefault() ?? string.Empty,
            XElement child => Normalize(child),
            _ => string.Empty,
        }));
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? NullIfEmpty(this string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}

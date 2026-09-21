// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Xml.Linq;
using System.Threading;
using System;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

internal static class XmlDocumentation
{
    public static string? Summary(ISymbol symbol, CancellationToken cancellationToken = default)
    {
        return TryGetDescription(symbol, out var description)
            ? description
            : Read(symbol, "summary", cancellationToken);
    }

    public static string? Remarks(ISymbol symbol, CancellationToken cancellationToken = default)
    {
        return TryGetDescription(symbol, out _)
            ? null
            : Read(symbol, "remarks", cancellationToken);
    }

    private static bool TryGetDescription(ISymbol symbol, out string? description)
    {
        var attribute = symbol.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass?.ToDisplayString() == "System.ComponentModel.DescriptionAttribute");
        description = attribute?.ConstructorArguments.FirstOrDefault().Value as string;
        if (description is not null)
            description = string.Join(' ', description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return attribute is not null;
    }

    private static string? Read(ISymbol symbol, string elementName, CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var element = XDocument.Parse(xml).Root?.Element(elementName);
            return element is null ? null : Normalize(element);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? Normalize(XElement element)
    {
        var text = string.Concat(element.Nodes().Select(node => node switch
        {
            XText value => value.Value,
            XElement child when child.Name.LocalName == "see"
                => child.Attribute("cref")?.Value.TrimStart("!:".ToCharArray()).Split('.').LastOrDefault() ?? string.Empty,
            XElement child => Normalize(child) ?? string.Empty,
            _ => string.Empty,
        }));
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

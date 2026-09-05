// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.Generators;

/// <summary>Validates static contract facts required by native messaging codecs.</summary>
public static class MessagingContractTopologyValidator
{
    private const int _messagePackProtocol = 1;
    private const int _protobufProtocol = 2;

    internal static readonly DiagnosticDescriptor _missingMessagePackShape = new(
        "ARKMSG025",
        "MessagePack contract shape is missing",
        "Contract '{0}' is used by participant '{1}' with effective protocol MessagePack and must declare MessagePack.MessagePackObjectAttribute",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor _missingProtobufShape = new(
        "ARKMSG026",
        "Google.Protobuf contract shape is missing",
        "Contract '{0}' is used by participant '{1}' with effective protocol Protobuf and must implement Google.Protobuf.IMessage<T> and expose the generated parser shape",
        "Ark.Tools.MediatorFramework",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static void _validate(
        Action<DiagnosticDescriptor, Location, object[]> report,
        INamedTypeSymbol contract,
        INamedTypeSymbol owner,
        int protocol)
    {
        if (protocol == _messagePackProtocol && !_hasMessagePackAttribute(contract))
        {
            report(
                _missingMessagePackShape,
                contract.Locations.FirstOrDefault() ?? Location.None,
                new object[] { contract.ToDisplayString(), owner.ToDisplayString() });
        }
        else if (protocol == _protobufProtocol && !_hasGoogleProtobufShape(contract))
        {
            report(
                _missingProtobufShape,
                contract.Locations.FirstOrDefault() ?? Location.None,
                new object[] { contract.ToDisplayString(), owner.ToDisplayString() });
        }
    }

    private static bool _hasMessagePackAttribute(INamedTypeSymbol contract)
    {
        return contract.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "MessagePack.MessagePackObjectAttribute");
    }

    private static bool _hasGoogleProtobufShape(INamedTypeSymbol contract)
    {
        var hasMessageInterface = contract.AllInterfaces.Any(_isGoogleProtobufMessage);
        var hasTypedMessageInterface = contract.AllInterfaces.Any(@interface =>
            _isGoogleProtobufMessage(@interface)
            && @interface.OriginalDefinition.MetadataName == "IMessage`1"
            && @interface.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], contract));
        var parser = contract.GetMembers("Parser").OfType<IPropertySymbol>().Any(property =>
            property.IsStatic
            && property.Type is INamedTypeSymbol parserType
            && parserType.OriginalDefinition.MetadataName == "MessageParser`1"
            && parserType.OriginalDefinition.ContainingNamespace.ToDisplayString() == "Google.Protobuf"
            && parserType.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(parserType.TypeArguments[0], contract));
        return hasMessageInterface && hasTypedMessageInterface && parser;
    }

    private static bool _isGoogleProtobufMessage(INamedTypeSymbol @interface)
    {
        return @interface.OriginalDefinition.ContainingNamespace.ToDisplayString() == "Google.Protobuf"
            && @interface.OriginalDefinition.MetadataName is "IMessage" or "IMessage`1";
    }
}

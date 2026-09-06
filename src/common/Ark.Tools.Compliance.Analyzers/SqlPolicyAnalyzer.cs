// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Requires explicit SQL column policies and lawful-purpose declarations at transport boundaries.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlPolicyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _missingColumn = new(
        "ARKPII007", "Declare storage protection for a classified column",
        "Classified member '{0}' has no SqlColumnPolicy on a SqlDataPolicy type; declare its verbatim column name and storage protection",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _missingEgress = new(
        "ARKPII012", "Declare the purpose of classified data leaving the application",
        "Contract '{0}' exposes classified data over {1} without a declared purpose; apply PersonalDataEgress(Purpose = ...) to the contract or boundary",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(_missingColumn, _missingEgress);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                    "build_property.EnableArkToolsCompliance", out var enabled)
                && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            start.RegisterSymbolAction(_analyzeType, SymbolKind.NamedType);
            start.RegisterSymbolAction(_analyzeEndpoint, SymbolKind.Method);
            start.RegisterOperationAction(_analyzeTransport, OperationKind.Invocation);
        });
    }

    private static void _analyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (_hasAttribute(type, "Ark.Tools.Compliance.Sql.SqlDataPolicyAttribute"))
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers().Where(static m => !m.IsStatic && !m.IsImplicitlyDeclared
                             && m.Kind is SymbolKind.Property or SymbolKind.Field))
                {
                    var valueType = ComplianceSymbolFacts._getValueType(member);
                    if ((ComplianceSymbolFacts._isClassified(member)
                         || ComplianceSymbolFacts._isClassified(type)
                         || (valueType is not null && ComplianceSymbolFacts._isClassified(ComplianceSymbolFacts._unwrapNullable(valueType))))
                        && !_hasAttribute(member, "Ark.Tools.Compliance.Sql.SqlColumnPolicyAttribute"))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(_missingColumn,
                            member.Locations.FirstOrDefault(static l => l.IsInSource) ?? type.Locations.FirstOrDefault(),
                            member.ToDisplayString()));
                    }
                }
            }
        }

        if (_hasAttribute(type, "Ark.Tools.MediatorFramework.HttpEndpointAttribute"))
        {
            foreach (var contract in type.AllInterfaces.Where(static i =>
                         i.Name is "IQuery" or "IRequest" && i.TypeArguments.Length > 0
                         && i.ContainingNamespace.ToDisplayString() is "Ark.Tools.Solid" or "Ark.Tools.MediatorFramework" or "Mediator")
                         .Select(static i => i.TypeArguments.Last()).Distinct(SymbolEqualityComparer.Default).OfType<ITypeSymbol>())
            {
                _reportEgress(context, type, contract, "HTTP", type.Locations.FirstOrDefault());
            }
            _reportEgress(context, type, type, "HTTP", type.Locations.FirstOrDefault());
        }
    }

    private static void _analyzeEndpoint(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.IsImplicitlyDeclared || method.MethodKind != MethodKind.Ordinary
            || method.DeclaredAccessibility != Accessibility.Public || method.IsStatic
            || _hasAttribute(method, "Microsoft.AspNetCore.Mvc.NonActionAttribute")
            || (!_isHttpMethod(method) && !_isController(method.ContainingType)))
        {
            return;
        }
        _reportEgress(context, method, method.ReturnType, "HTTP", method.Locations.FirstOrDefault());
        foreach (var parameter in method.Parameters)
        {
            _reportEgress(context, method, parameter.Type, "HTTP", parameter.Locations.FirstOrDefault(),
                ComplianceSymbolFacts._isClassified(parameter), parameter);
        }
    }

    private static bool _isHttpMethod(IMethodSymbol method)
    {
        return method.GetAttributes().Any(static a =>
            a.AttributeClass?.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Mvc"
            && a.AttributeClass.Name.StartsWith("Http", StringComparison.Ordinal));
    }

    private static bool _isController(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "Microsoft.AspNetCore.Mvc.ControllerBase")
            {
                return true;
            }
        }
        return false;
    }

    private static void _analyzeTransport(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var owner = method.ContainingType.ToDisplayString();
        var channel = owner == "System.Text.Json.JsonSerializer" && method.Name.StartsWith("Serialize", StringComparison.Ordinal)
            || owner == "Newtonsoft.Json.JsonConvert" && method.Name == "SerializeObject"
            ? "JSON"
            : method.ContainingNamespace.ToDisplayString().StartsWith("Rebus.", StringComparison.Ordinal)
              && method.Name is "Send" or "SendLocal" or "Publish" or "Reply"
                ? "messaging"
                : null;
        if (channel is null || _hasEgressPolicy(context.ContainingSymbol))
        {
            return;
        }
        foreach (var argument in invocation.Arguments)
        {
            var value = argument.Value;
            while (value is IConversionOperation conversion)
            {
                value = conversion.Operand;
            }
            var type = value.Type;
            var source = value switch
            {
                IPropertyReferenceOperation property => (ISymbol)property.Property,
                IFieldReferenceOperation field => field.Field,
                IParameterReferenceOperation parameter => parameter.Parameter,
                _ => null,
            };
            if (type is null || (!ComplianceSymbolFacts._isClassified(source)
                    && !_containsClassified(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
                || _hasEgressPolicy(type) || (source is not null && _hasEgressPolicy(source)))
            {
                continue;
            }
            context.ReportDiagnostic(Diagnostic.Create(_missingEgress, argument.Syntax.GetLocation(),
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), channel));
        }
    }

    private static void _reportEgress(SymbolAnalysisContext context, ISymbol boundary, ITypeSymbol contract,
        string channel, Location? location, bool classified = false, ISymbol? parameter = null)
    {
        if (!_hasEgressPolicy(boundary) && !_hasEgressPolicy(contract)
            && (parameter is null || !_hasEgressPolicy(parameter))
            && (classified || _containsClassified(contract, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default))))
        {
            context.ReportDiagnostic(Diagnostic.Create(_missingEgress, location,
                contract.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), channel));
        }
    }

    private static bool _containsClassified(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (_hasEgressPolicy(type) || visited.Count >= 64 || !visited.Add(type))
        {
            return false;
        }
        if (ComplianceSymbolFacts._isClassified(type))
        {
            return true;
        }
        if (type is IArrayTypeSymbol array)
        {
            return _containsClassified(array.ElementType, visited);
        }
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }
        if (named.TypeArguments.Any(t => !_hasEgressPolicy(t) && _containsClassified(t, visited)))
        {
            return true;
        }
        for (var current = named; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers().Where(static m => !m.IsStatic && !m.IsImplicitlyDeclared
                         && m.DeclaredAccessibility == Accessibility.Public
                         && m.Kind is SymbolKind.Property or SymbolKind.Field))
            {
                if (_hasEgressPolicy(member))
                {
                    continue;
                }
                if (ComplianceSymbolFacts._isClassified(member))
                {
                    return true;
                }
                var memberType = ComplianceSymbolFacts._getValueType(member);
                if (memberType is not null && !_hasEgressPolicy(memberType) && _containsClassified(memberType, visited))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool _hasEgressPolicy(ISymbol symbol)
    {
        for (var current = symbol; current is not null && current.Kind != SymbolKind.Namespace; current = current.ContainingSymbol)
        {
            if (current.GetAttributes().Any(static a =>
                    a.AttributeClass?.ToDisplayString() == "Ark.Tools.Compliance.PersonalDataEgressAttribute"
                    && a.NamedArguments.Any(static p => p.Key == "Purpose"
                        && p.Value.Value is string purpose && !string.IsNullOrWhiteSpace(purpose))))
            {
                return true;
            }
        }
        return false;
    }

    private static bool _hasAttribute(ISymbol symbol, string name)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);
    }
}

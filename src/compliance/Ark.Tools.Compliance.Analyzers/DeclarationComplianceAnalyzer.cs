// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Requires explicit classification and reviewed, redaction-safe declarations.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeclarationComplianceAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor _unclassified = new(
        "ARKPII001", "Declare personal data before it escapes redaction",
        "Member '{0}' looks like personal data but is not classified; unclassified data escapes redaction and the compliance inventory",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII001.md");

    internal static readonly DiagnosticDescriptor _review = new(
        "ARKPII008", "Keep compliance reviews justified and current",
        "Compliance review on '{0}' has no reason or has an invalid or expired Expires date; an unreviewed exception can expose personal data",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII008.md");

    internal static readonly DiagnosticDescriptor _justification = new(
        "ARKPII009", "Explain why a declaration contains no personal data",
        "NotPersonalData on '{0}' needs a specific justification, not a placeholder; incorrectly excluded data escapes redaction",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII009.md");

    internal static readonly DiagnosticDescriptor _unsupported = new(
        "ARKPII010", "Use a classification shape that can be safely redacted",
        "Classified declaration '{0}' cannot be safely redacted: {1}",
        "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII010.md");

    internal static readonly DiagnosticDescriptor _missingRedactionRegistration = new(
        "ARKPII013", "Register Ark redaction for Microsoft telemetry",
        "Project references Microsoft.Extensions.Telemetry but does not call AddArkRedaction(); classified logging can remain unredacted",
        "Compliance", DiagnosticSeverity.Warning, isEnabledByDefault: true, customTags: ["CompilationEnd"], helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII013.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(_unclassified, _review, _justification, _unsupported, _missingRedactionRegistration);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var options = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (!ComplianceCompilationFacts._isEnabled(options))
            {
                return;
            }

            var facts = ComplianceCompilationFacts._create(
                start.Compilation,
                options);

            var lexicon = new ComplianceLexicon(start.Options.AdditionalFiles, start.CancellationToken);
            var today = DateTime.UtcNow.Date;
            var hasRedactionRegistration = 0;
            var hasServiceCollectionSetup = 0;
            if (facts._telemetryRequiresRegistration)
            {
                var serviceCollectionType = start.Compilation.GetTypeByMetadataName(
                    "Microsoft.Extensions.DependencyInjection.IServiceCollection");
                start.RegisterOperationAction(operationContext =>
                {
                    var invocation = (Microsoft.CodeAnalysis.Operations.IInvocationOperation)operationContext.Operation;
                    var method = invocation.TargetMethod;
                    if (method.Name == "AddArkRedaction"
                        && ComplianceSymbolFacts._isArkToolsComplianceNamespace(method.ContainingNamespace))
                    {
                        Interlocked.Exchange(ref hasRedactionRegistration, 1);
                    }
                }, Microsoft.CodeAnalysis.OperationKind.Invocation);
                if (serviceCollectionType is not null)
                {
                    start.RegisterOperationAction(operationContext =>
                    {
                        var creation = (Microsoft.CodeAnalysis.Operations.IObjectCreationOperation)operationContext.Operation;
                        if (_isServiceCollection(creation.Type, serviceCollectionType))
                        {
                            Interlocked.Exchange(ref hasServiceCollectionSetup, 1);
                        }
                    }, Microsoft.CodeAnalysis.OperationKind.ObjectCreation);
                    start.RegisterOperationAction(operationContext =>
                    {
                        var property = (Microsoft.CodeAnalysis.Operations.IPropertyReferenceOperation)operationContext.Operation;
                        if (_isServiceCollection(property.Type, serviceCollectionType))
                        {
                            Interlocked.Exchange(ref hasServiceCollectionSetup, 1);
                        }
                    }, Microsoft.CodeAnalysis.OperationKind.PropertyReference);
                    start.RegisterOperationAction(operationContext =>
                    {
                        var parameter = (Microsoft.CodeAnalysis.Operations.IParameterReferenceOperation)operationContext.Operation;
                        if (_isServiceCollection(parameter.Type, serviceCollectionType))
                        {
                            Interlocked.Exchange(ref hasServiceCollectionSetup, 1);
                        }
                    }, Microsoft.CodeAnalysis.OperationKind.ParameterReference);
                }
                start.RegisterCompilationEndAction(endContext =>
                {
                    if (Volatile.Read(ref hasServiceCollectionSetup) != 0
                        && Volatile.Read(ref hasRedactionRegistration) == 0)
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(_missingRedactionRegistration, Location.None));
                    }
                });
            }
            start.RegisterSymbolAction(symbolContext =>
            {
                _analyze(symbolContext, symbolContext.Symbol, lexicon, today, facts);
                if (symbolContext.Symbol is IMethodSymbol method
                    && method.MethodKind is not MethodKind.PropertyGet and not MethodKind.PropertySet)
                {
                    foreach (var parameter in method.Parameters)
                    {
                        _analyze(symbolContext, parameter, lexicon, today, facts);
                    }
                }
                else if (symbolContext.Symbol is IPropertySymbol property)
                {
                    foreach (var parameter in property.Parameters)
                    {
                        _analyze(symbolContext, parameter, lexicon, today, facts);
                    }
                }
            }, SymbolKind.Property, SymbolKind.Field, SymbolKind.Method, SymbolKind.NamedType);
        });
    }

    private static bool _isServiceCollection(ITypeSymbol? type, INamedTypeSymbol serviceCollectionType)
    {
        return type is INamedTypeSymbol named
            && (SymbolEqualityComparer.Default.Equals(named, serviceCollectionType)
                || named.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, serviceCollectionType)));
    }

    private static void _analyze(
        SymbolAnalysisContext context,
        ISymbol symbol,
        ComplianceLexicon lexicon,
        DateTime today,
        ComplianceCompilationFacts facts)
    {
        if (symbol.IsImplicitlyDeclared)
        {
            return;
        }

        var location = symbol.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        if (location is null)
        {
            return;
        }

        Dictionary<ISymbol, bool>? classifiedCache = null;
        bool isClassified(ISymbol? candidate)
        {
            if (candidate is null)
            {
                return false;
            }

            classifiedCache ??= new Dictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
            if (!classifiedCache.TryGetValue(candidate, out var result))
            {
                result = ComplianceSymbolFacts._isClassified(candidate, facts);
                classifiedCache[candidate] = result;
            }

            return result;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            if (ComplianceSymbolFacts._matchesAttribute(attribute, facts._complianceReviewedAttribute)
                && !ComplianceSymbolFacts._isReviewValid(attribute, today))
            {
                context.ReportDiagnostic(Diagnostic.Create(_review, location, symbol.Name));
            }
            else if (ComplianceSymbolFacts._matchesAttribute(attribute, facts._notPersonalDataAttribute)
                && !ComplianceSymbolFacts._isJustificationMeaningful(
                    attribute.ConstructorArguments.FirstOrDefault().Value as string))
            {
                context.ReportDiagnostic(Diagnostic.Create(_justification, location, symbol.Name));
            }
        }

        var type = ComplianceSymbolFacts._getValueType(symbol);
        if (type is not null)
        {
            type = ComplianceSymbolFacts._unwrapNullable(type);
            var positionalCounterpart = _positionalCounterpart(symbol);
            var isPositionalProperty = symbol is IPropertySymbol && positionalCounterpart is not null;
            var knownSafeDotNetType = ComplianceSymbolFacts._isKnownSafeDotNetType(type, facts);
            var classified = isClassified(symbol)
                || isClassified(type)
                || isClassified(symbol.ContainingType)
                || isClassified(positionalCounterpart);
            if (!isPositionalProperty && !knownSafeDotNetType && !classified && lexicon._matches(symbol.Name)
                && !ComplianceSymbolFacts._hasAttribute(symbol, facts._notPersonalDataAttribute)
                && !_hasPositionalExclusion(positionalCounterpart, facts))
            {
                context.ReportDiagnostic(Diagnostic.Create(_unclassified, location, symbol.Name));
            }

            if (classified && !isPositionalProperty)
            {
                _checkType(context, symbol, type, location, facts);
            }
        }
        else if (symbol is INamedTypeSymbol named && isClassified(named))
        {
            _checkType(context, symbol, named, location, facts);
        }
    }

    private static ISymbol? _positionalCounterpart(ISymbol symbol)
    {
        if (symbol.ContainingType?.IsRecord != true)
        {
            return null;
        }

        if (symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } })
        {
            return symbol.ContainingType.GetMembers(symbol.Name).OfType<IPropertySymbol>()
                .FirstOrDefault(property => property.Locations.Any(location => symbol.Locations.Contains(location)));
        }

        if (symbol is IPropertySymbol propertySymbol)
        {
            return symbol.ContainingType.InstanceConstructors.SelectMany(static constructor => constructor.Parameters)
                .FirstOrDefault(parameter => parameter.Name == symbol.Name
                    && parameter.Locations.Any(location => propertySymbol.Locations.Contains(location)));
        }

        return null;
    }

    private static bool _hasPositionalExclusion(ISymbol? counterpart, ComplianceCompilationFacts facts)
    {
        return counterpart is not null
            && ComplianceSymbolFacts._hasAttribute(counterpart, facts._notPersonalDataAttribute);
    }

    private static void _checkType(
        SymbolAnalysisContext context,
        ISymbol symbol,
        ITypeSymbol type,
        Location location,
        ComplianceCompilationFacts facts)
    {
        var knownSafeDotNetType = ComplianceSymbolFacts._isKnownSafeDotNetType(type, facts);
        if (knownSafeDotNetType)
        {
            return;
        }

        var risk = type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic
            ? "replace open object/dynamic with a concrete classified type"
            : type.TypeKind == TypeKind.Delegate
                ? "a delegate is executable code, not a redactable value"
                : type is INamedTypeSymbol named ? _vogenRisks(named, facts) : null;
        if (risk is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_unsupported, location, symbol.Name, risk));
        }
    }

    private static string? _vogenRisks(INamedTypeSymbol type, ComplianceCompilationFacts facts)
    {
        var valueObject = type.GetAttributes().FirstOrDefault(attribute =>
            ComplianceSymbolFacts._matchesAttribute(attribute, facts._vogenValueObjectAttribute));
        if (valueObject is null)
        {
            return null;
        }

        var risks = new List<string>();
        var options = new Dictionary<string, TypedConstant>(StringComparer.OrdinalIgnoreCase);
        if (valueObject.AttributeConstructor is { } constructor)
        {
            for (var index = 0; index < constructor.Parameters.Length && index < valueObject.ConstructorArguments.Length; index++)
            {
                options[constructor.Parameters[index].Name] = valueObject.ConstructorArguments[index];
            }
        }

        foreach (var option in valueObject.NamedArguments)
        {
            options[option.Key] = option.Value;
        }

        if (!options.TryGetValue("conversions", out var conversions)
            || _containsEnumOption(conversions, "Default", "TypeConverter"))
        {
            risks.Add("set Conversions.None (TypeConverter.ConvertTo exposes cleartext)");
        }

        if (!options.TryGetValue("debuggerAttributes", out var debugger)
            || !_isEnumOption(debugger, "None", "Omit"))
        {
            risks.Add("disable DebuggerAttributeGeneration; Vogen versions without None require an Ark sensitive value object");
        }

        foreach (var option in options.Where(static option =>
            option.Key.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0
            && _isEnumOption(option.Value, "Implicit")))
        {
            risks.Add("set " + option.Key + " = CastOperator.None (implicit conversion exposes cleartext)");
        }

        if (!type.GetMembers("ToString").OfType<IMethodSymbol>().Any(static method =>
                method.Parameters.Length == 0 && method.IsOverride && _isUserMethod(method)))
        {
            risks.Add("supply a redacted ToString override instead of the generated ToString");
        }

        if (!type.GetMembers("TryFormat").OfType<IMethodSymbol>().Any(static method => _isUserMethod(method)))
        {
            risks.Add("supply a redacted TryFormat implementation");
        }

        if (type.GetMembers().OfType<IMethodSymbol>().Any(static method => method.Name == "op_Implicit"))
        {
            risks.Add("remove implicit conversion operators");
        }

        return risks.Count == 0 ? null : string.Join("; ", risks);
    }

    private static bool _isUserMethod(IMethodSymbol method)
    {
        return method.DeclaringSyntaxReferences.Any(static reference =>
            !reference.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            && !reference.SyntaxTree.FilePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase));
    }

    private static bool _isEnumOption(TypedConstant value, params string[] names)
    {
        return value.Type?.GetMembers().OfType<IFieldSymbol>().Any(field =>
            field.HasConstantValue && Equals(field.ConstantValue, value.Value)
            && names.Contains(field.Name, StringComparer.OrdinalIgnoreCase)) == true;
    }

    private static bool _containsEnumOption(TypedConstant value, params string[] names)
    {
        if (value.Value is null)
        {
            return true;
        }

        var bits = Convert.ToInt64(value.Value, CultureInfo.InvariantCulture);
        return value.Type?.GetMembers().OfType<IFieldSymbol>().Any(field =>
        {
            if (!field.HasConstantValue || !names.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            var flag = Convert.ToInt64(field.ConstantValue, CultureInfo.InvariantCulture);
            return flag == 0 ? bits == 0 : (bits & flag) == flag;
        }) == true;
    }
}

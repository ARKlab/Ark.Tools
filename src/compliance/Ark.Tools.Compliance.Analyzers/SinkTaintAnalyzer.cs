// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Tools.Compliance.Analyzers;

/// <summary>Rejects classified values at unbounded sinks using bounded, intra-method operation reachability.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SinkTaintAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _log = _descriptor("ARKPII002", "Classified data reaches a log", "a log template, argument, or scope");
    private static readonly DiagnosticDescriptor _exception = _descriptor("ARKPII003", "Classified data reaches an exception", "an exception message, data, or error contract");
    private static readonly DiagnosticDescriptor _telemetry = _descriptor("ARKPII004", "Classified data reaches telemetry", "an Activity tag, metric dimension, or baggage");
    private static readonly DiagnosticDescriptor _format = _descriptor("ARKPII005", "Classified data is formatted without protection", "unredacted formatting or Reveal without a purpose");
    private static readonly DiagnosticDescriptor _banned = _descriptor("ARKPII011", "Classified data reaches a formatting sink", "a banned formatting sink");
    private readonly Func<DateTime> _utcNow;

    /// <summary>Initializes a new instance of the <see cref="SinkTaintAnalyzer"/> class.</summary>
    public SinkTaintAnalyzer()
        : this(static () => DateTime.UtcNow)
    {
    }

    internal SinkTaintAnalyzer(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_log, _exception, _telemetry, _format, _banned];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "build_property.EnableArkToolsCompliance", out var enabled)
                && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var sinks = SinkConfiguration._read(start.Options, start.CancellationToken);
            var today = _utcNow().Date;
            start.RegisterOperationAction(ctx => _analyze(ctx, sinks, today),
                OperationKind.Invocation, OperationKind.ObjectCreation, OperationKind.InterpolatedString,
                OperationKind.Binary, OperationKind.Conversion, OperationKind.SimpleAssignment,
                OperationKind.CompoundAssignment);
            start.RegisterSymbolAction(ctx => _analyzeErrorContract(ctx, today), SymbolKind.Property, SymbolKind.Field);
        });
    }

    private static DiagnosticDescriptor _descriptor(string id, string title, string sink)
    {
        return new DiagnosticDescriptor(id, title,
            "Classified member '{0}' ([{1}]) reaches " + sink + "; log the key, not the person, or pass the value through a redactor. Analysis is intra-method only",
            "Compliance", DiagnosticSeverity.Error, isEnabledByDefault: true,
            description: "Tracks classified values through bounded intra-method IOperation reachability; cross-method flow is intentionally not analyzed.",
            helpLinkUri: $"https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/{id}.md");
    }

    private static void _analyze(OperationAnalysisContext context, SinkConfiguration sinks, DateTime today)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        switch (context.Operation)
        {
            case IInvocationOperation invocation:
                _invocation(context, invocation, sinks, today);
                break;
            case IObjectCreationOperation { Constructor: not null } creation:
                var rule = sinks._getRule(creation.Constructor);
                if (rule is not null)
                {
                    foreach (var argument in creation.Arguments)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        if (rule != "ARKPII003"
                            || !SinkConfiguration._isOrDerivesFrom(argument.Parameter?.Type as INamedTypeSymbol, "System.Exception"))
                        {
                            _check(context, argument.Value, rule, today);
                        }
                    }
                }

                break;
            case IInterpolatedStringOperation interpolation:
                if (!_isInsideConfiguredSink(interpolation, sinks, context.CancellationToken))
                {
                    _check(context, interpolation, "ARKPII005", today);
                }
                break;
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } binary:
                if (!_isInsideConfiguredSink(binary, sinks, context.CancellationToken))
                {
                    _check(context, binary, "ARKPII005", today);
                }
                break;
            case IConversionOperation { IsImplicit: true, Type.SpecialType: SpecialType.System_String } conversion:
                if (!_isInsideConfiguredSink(conversion, sinks, context.CancellationToken))
                {
                    _check(context, conversion.Operand, "ARKPII005", today);
                }
                break;
            case IAssignmentOperation assignment:
                if (_isExceptionData(assignment.Target, context, 0))
                {
                    _check(context, assignment.Value, "ARKPII003", today);
                    if (assignment.Target is IPropertyReferenceOperation indexer)
                    {
                        foreach (var argument in indexer.Arguments)
                        {
                            context.CancellationToken.ThrowIfCancellationRequested();
                            _check(context, argument.Value, "ARKPII003", today);
                        }
                    }
                }

                if (assignment is ICompoundAssignmentOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String })
                {
                    _check(context, assignment.Value, "ARKPII005", today);
                    _check(context, assignment.Target, "ARKPII005", today);
                }

                break;
        }
    }

    private static void _invocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SinkConfiguration sinks,
        DateTime today)
    {
        var method = invocation.TargetMethod;
        var rule = sinks._getRule(method);
        if (_isExceptionData(invocation.Instance, context, 0) && method.Name is "Add" or "set_Item")
        {
            rule = "ARKPII003";
        }

        if (rule is not null)
        {
            foreach (var argument in invocation.Arguments)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (rule == "ARKPII004"
                    && ComplianceSymbolFacts._isNamespace(method.ContainingNamespace, "System.Diagnostics.Metrics")
                    && argument.Parameter?.Ordinal == 0)
                {
                    continue;
                }

                // The receiver of an extension sink is the sink itself (logger, activity, builder), never
                // the logged data; instance sinks are already exempt because Instance is not checked.
                if (_isExtensionReceiver(invocation, argument))
                {
                    continue;
                }

                _check(context, argument.Value, rule, today);
            }
        }

        if (method.Name == "Reveal")
        {
            if (!_hasPurpose(invocation, context))
            {
                if (invocation.Instance is not null)
                {
                    if (SinkFlow._isSelfProtecting(invocation.Instance.Type, context.CancellationToken)
                        && invocation.Instance.Type is not null
                        && !_reviewed(context.ContainingSymbol, "ARKPII005", today, context.CancellationToken))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(_format, invocation.Syntax.GetLocation(),
                            invocation.Instance.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), "SensitiveValue"));
                    }
                    else
                    {
                        _check(context, invocation.Instance, "ARKPII005", today);
                    }
                }

                foreach (var argument in invocation.Arguments)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    _check(context, argument.Value, "ARKPII005", today);
                }
            }
        }
        else if (method.Name == "ToString" && invocation.Instance is not null
            && !_isInsideConfiguredSink(invocation, sinks, context.CancellationToken))
        {
            _check(context, invocation.Instance, "ARKPII005", today);
        }
        else if (method.ContainingType.SpecialType == SpecialType.System_String
            && (method.Name is "Concat" or "Format")
            && !_isInsideConfiguredSink(invocation, sinks, context.CancellationToken))
        {
            _check(context, invocation, "ARKPII005", today);
        }
    }

    private static bool _hasPurpose(IInvocationOperation invocation, OperationAnalysisContext context)
    {
        foreach (var argument in invocation.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (ComplianceSymbolFacts._isFullName(argument.Parameter?.Type as INamedTypeSymbol, "Ark.Tools.Compliance.CompliancePurpose")
                && argument.ArgumentKind != ArgumentKind.DefaultValue
                && !_emptyPurpose(argument.Value, context, 0))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The receiver of a framework extension sink is the sink itself (an <c>ILogger</c>, an activity, a
    /// builder), and it only taints through an unrelated generic argument such as <c>ILogger&lt;TCategory&gt;</c>.
    /// The exemption stops at framework receivers on purpose: sink configuration accepts arbitrary method
    /// IDs, so a consumer extension declared on its own classified type must stay reportable.
    /// </summary>
    private static bool _isExtensionReceiver(IInvocationOperation invocation, IArgumentOperation argument)
    {
        return invocation.TargetMethod.IsExtensionMethod
            && invocation.Instance is null
            && argument.Parameter?.Ordinal == 0
            && SinkFlow._isFrameworkType(argument.Parameter.Type);
    }

    private static bool _isInsideConfiguredSink(
        IOperation operation,
        SinkConfiguration sinks,
        CancellationToken cancellationToken)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (parent is IInvocationOperation invocation && sinks._getRule(invocation.TargetMethod) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool _emptyPurpose(IOperation operation, OperationAnalysisContext context, int depth, HashSet<ILocalSymbol>? visited = null)
    {
        if (depth >= 64)
        {
            return false;
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        if (operation is ILocalReferenceOperation local)
        {
            visited ??= new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
            if (!visited.Add(local.Local))
            {
                return false;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(local.Syntax.SyntaxTree);
            foreach (var value in new SinkFlow(options, context.CancellationToken)._localValues(local))
            {
                if (_emptyPurpose(value, context, depth + 1, visited))
                {
                    return true;
                }
            }

            return false;
        }

        return operation is IDefaultValueOperation
            || operation is IObjectCreationOperation { Arguments.Length: 0 }
            || operation.ConstantValue is { HasValue: true, Value: null };
    }

    private static bool _isExceptionData(IOperation? operation, OperationAnalysisContext context, int depth, HashSet<ILocalSymbol>? visited = null)
    {
        for (; operation is not null && depth < 64; depth++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (operation is IPropertyReferenceOperation property)
            {
                if (property.Property.Name == "Data"
                    && SinkConfiguration._isOrDerivesFrom(property.Property.ContainingType, "System.Exception"))
                {
                    return true;
                }

                operation = property.Instance;
            }
            else if (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }
            else if (operation is ILocalReferenceOperation local)
            {
                visited ??= new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
                if (!visited.Add(local.Local))
                {
                    return false;
                }

                var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(local.Syntax.SyntaxTree);
                var nextDepth = depth + 1;
                foreach (var value in new SinkFlow(options, context.CancellationToken)._localValues(local))
                {
                    if (_isExceptionData(value, context, nextDepth, visited))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    private static void _check(OperationAnalysisContext context, IOperation value, string rule, DateTime today)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(value.Syntax.SyntaxTree);
        var source = new SinkFlow(options, context.CancellationToken)._find(value);
        if (source is null
            || _reviewed(context.ContainingSymbol, rule, today, context.CancellationToken)
            || _reviewed(source._symbol, rule, today, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_getDescriptor(rule), value.Syntax.GetLocation(),
            source._symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), source._classification));
    }

    private static DiagnosticDescriptor _getDescriptor(string rule)
    {
        return rule switch
        {
            "ARKPII002" => _log,
            "ARKPII003" => _exception,
            "ARKPII004" => _telemetry,
            "ARKPII005" => _format,
            _ => _banned,
        };
    }

    private static void _analyzeErrorContract(SymbolAnalysisContext context, DateTime today)
    {
        var symbol = context.Symbol;
        var type = symbol.ContainingType;
        var isErrorContract = false;
        for (; type is not null; type = type.BaseType)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (type.Name == "BusinessRuleViolation" && _isArkRootedNamespace(type.ContainingNamespace))
            {
                isErrorContract = true;
                break;
            }
        }

        if (!isErrorContract || symbol.IsImplicitlyDeclared
            || _reviewed(symbol, "ARKPII003", today, context.CancellationToken))
        {
            return;
        }

        Location? location = null;
        foreach (var candidate in symbol.Locations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (candidate.IsInSource)
            {
                location = candidate;
                break;
            }
        }

        if (location?.SourceTree is null)
        {
            return;
        }

        var memberType = symbol is IPropertySymbol property ? property.Type : ((IFieldSymbol)symbol).Type;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(location.SourceTree);
        var source = new SinkFlow(options, context.CancellationToken)._find(symbol, memberType);
        if (source is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_exception, location,
                source._symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), source._classification));
        }
    }

    /// <summary>Equivalent of <c>ns.ToDisplayString().StartsWith("Ark.")</c>: root segment "Ark" with at least one child segment.</summary>
    private static bool _isArkRootedNamespace(INamespaceSymbol? @namespace)
    {
        var hasChild = false;
        while (@namespace is { IsGlobalNamespace: false })
        {
            if (@namespace.ContainingNamespace is { IsGlobalNamespace: true })
            {
                return hasChild && @namespace.Name == "Ark";
            }

            hasChild = true;
            @namespace = @namespace.ContainingNamespace;
        }

        return false;
    }

    private static bool _reviewed(
        ISymbol? symbol,
        string rule,
        DateTime today,
        CancellationToken cancellationToken)
    {
        // An accessor's ContainingSymbol is the type, not the property/event it belongs to, so the
        // walk goes through AssociatedSymbol first; otherwise a review declared on a property (an
        // AttributeUsage target of ComplianceReviewedAttribute) would never reach its accessor bodies.
        for (; symbol is not null; symbol = (symbol as IMethodSymbol)?.AssociatedSymbol ?? symbol.ContainingSymbol)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var attribute in symbol.GetAttributes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ComplianceSymbolFacts._isFullName(attribute.AttributeClass, "Ark.Tools.Compliance.ComplianceReviewedAttribute")
                    || attribute.ConstructorArguments.Length < 2
                    || attribute.ConstructorArguments[0].Value is not string id || id != rule
                    || attribute.ConstructorArguments[1].Value is not string reason || string.IsNullOrWhiteSpace(reason))
                {
                    continue;
                }

                string? expires = null;
                foreach (var pair in attribute.NamedArguments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (pair.Key == "Expires")
                    {
                        expires = pair.Value.Value as string;
                        break;
                    }
                }

                if (expires is null || (DateTime.TryParseExact(expires, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) && date.Date >= today))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
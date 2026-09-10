// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ark.Tools.Compliance.Analyzers;

internal sealed class SinkFlow
{
    private readonly CancellationToken _cancellationToken;
    private readonly int _maxNodes;
    private readonly int _maxDepth;
    private readonly int _maxTypeDepth;
    private int _nodes;
    private bool _exhausted;

    internal SinkFlow(AnalyzerConfigOptions options, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _maxNodes = SinkConfiguration._getLimit(options, "ark_compliance.max_operation_nodes", 512, 4096);
        _maxDepth = SinkConfiguration._getLimit(options, "ark_compliance.max_operation_depth", 64, 128);
        _maxTypeDepth = SinkConfiguration._getLimit(options, "ark_compliance.max_type_depth", 5, 32);
    }

    internal Source? _find(IOperation operation)
    {
        var result = _find(operation, 0);
        return _exhausted ? null : result;
    }

    internal Source? _find(ISymbol symbol, ITypeSymbol? type)
    {
        var result = _classification(symbol)
            ?? (symbol is IPropertySymbol property ? _positionalClassification(property) : null)
            ?? _type(type, 0, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        return _exhausted ? null : result;
    }

    /// <summary>A sensitive value object renders redacted everywhere (ToString/TryFormat/debugger); only Reveal yields cleartext.</summary>
    internal static bool _isSelfProtecting(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments.FirstOrDefault();
        }

        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        if (named.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.OriginalDefinition.MetadataName == "SensitiveValueObjectAttribute`1"
            && attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == "Ark.Tools.Compliance"))
        {
            return true;
        }

        return _isSensitiveContract(named) || named.AllInterfaces.Any(_isSensitiveContract);
    }

    internal static bool _isRedacted(IOperation operation)
    {
        if (operation.Type?.ToDisplayString() == "Ark.Tools.Compliance.RedactedValue")
        {
            return true;
        }

        if (operation is not IInvocationOperation invocation || invocation.TargetMethod.Name != "Redact")
        {
            return false;
        }

        return SinkConfiguration._isOrDerivesFrom(invocation.TargetMethod.ContainingType, "Ark.Tools.Compliance.Redactor")
            || SinkConfiguration._isOrDerivesFrom(invocation.TargetMethod.ContainingType, "Microsoft.Extensions.Compliance.Redaction.Redactor");
    }

    private bool _enter(int depth)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (++_nodes > _maxNodes || depth > _maxDepth)
        {
            _exhausted = true;
        }

        return !_exhausted;
    }

    private Source? _find(IOperation? operation, int depth)
    {
        if (operation is null || !_enter(depth) || operation.ConstantValue.HasValue || _isRedacted(operation)
            || _isSelfProtecting(operation.Type))
        {
            return null;
        }

        var classifiedType = _type(operation.Type, 0, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        if (classifiedType is not null)
        {
            return classifiedType;
        }

        switch (operation)
        {
            case ILocalReferenceOperation local:
                return _classification(local.Local) ?? _local(local, depth + 1);
            case IParameterReferenceOperation parameter:
                return _classification(parameter.Parameter);
            case IPropertyReferenceOperation property:
                return _classification(property.Property)
                    ?? _positionalClassification(property.Property)
                    ?? (property.Property.IsIndexer
                        ? _find(property.Instance, depth + 1)
                        : _direct(property.Instance, depth + 1));
            case IFieldReferenceOperation field:
                return _classification(field.Field) ?? _direct(field.Instance, depth + 1);
            case IConditionalOperation conditional:
                return _find(conditional.WhenTrue, depth + 1) ?? _find(conditional.WhenFalse, depth + 1);
            case ICoalesceOperation coalesce:
                return _find(coalesce.Value, depth + 1) ?? _find(coalesce.WhenNull, depth + 1);
            case IConditionalAccessOperation conditional:
                return _find(conditional.WhenNotNull, depth + 1) ?? _direct(conditional.Operation, depth + 1);
            case IInvocationOperation invocation:
                if (invocation.TargetMethod.Name is "Reveal" or "ToString"
                    || invocation.TargetMethod.ContainingType.SpecialType == SpecialType.System_String
                    || invocation.Parent is IObjectOrCollectionInitializerOperation)
                {
                    return _find(invocation.Instance, depth + 1) ?? _children(invocation.Arguments, depth + 1);
                }

                // Method bodies are intentionally not followed. Classified return types were checked above.
                return _classification(invocation.TargetMethod);
            case IObjectCreationOperation creation:
                // Carrier objects preserve local reachability, including metric tags and ActivityEvent.
                return _children(creation.Arguments, depth + 1) ?? _find(creation.Initializer, depth + 1);
            case IArrayElementReferenceOperation element:
                return _find(element.ArrayReference, depth + 1);
            case IAnonymousFunctionOperation:
            case ILocalFunctionOperation:
                return null;
            case IBinaryOperation binary:
                return binary.Type?.SpecialType == SpecialType.System_String
                    ? _find(binary.LeftOperand, depth + 1) ?? _find(binary.RightOperand, depth + 1)
                    : null;
            default:
                return _children(operation.ChildOperations, depth + 1);
        }
    }

    private Source? _children(IEnumerable<IOperation> operations, int depth)
    {
        foreach (var source in operations.Select(child => _find(child, depth)))
        {
            if (source is not null || _exhausted)
            {
                return source;
            }
        }

        return null;
    }

    private Source? _direct(IOperation? operation, int depth)
    {
        if (operation is null || !_enter(depth) || _isSelfProtecting(operation.Type))
        {
            return null;
        }

        var source = operation.Type is null ? null : _classification(operation.Type);
        return source ?? operation switch
        {
            IPropertyReferenceOperation property => _classification(property.Property) ?? _direct(property.Instance, depth + 1),
            IFieldReferenceOperation field => _classification(field.Field) ?? _direct(field.Instance, depth + 1),
            IParameterReferenceOperation parameter => _classification(parameter.Parameter),
            ILocalReferenceOperation local => _directLocal(local, depth + 1),
            IConversionOperation conversion => _direct(conversion.Operand, depth + 1),
            _ => null,
        };
    }

    private Source? _directLocal(ILocalReferenceOperation local, int depth)
    {
        foreach (var source in _localValues(local, depth).Select(value => _direct(value, depth + 1)))
        {
            if (source is not null || _exhausted)
            {
                return source;
            }
        }

        return null;
    }

    private Source? _local(ILocalReferenceOperation reference, int depth)
    {
        return _children(_localValues(reference, depth), depth + 1);
    }

    internal IReadOnlyList<IOperation> _localValues(ILocalReferenceOperation reference, int depth = 0)
    {
        var root = (IOperation)reference;
        while (root.Parent is not (null or IAnonymousFunctionOperation or ILocalFunctionOperation))
        {
            if (!_enter(depth))
            {
                return Array.Empty<IOperation>();
            }

            root = root.Parent;
        }

        var pending = new Stack<IOperation>();
        var writes = new List<(IOperation Value, bool Conditional)>();
        pending.Push(root);
        while (pending.Count > 0 && !_exhausted)
        {
            var operation = pending.Pop();
            if (!_enter(depth))
            {
                return Array.Empty<IOperation>();
            }

            if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                continue;
            }

            if (operation.Syntax.Span.End <= reference.Syntax.SpanStart || _sameLoop(reference, operation))
            {
                if (operation is IVariableDeclaratorOperation declaration
                    && SymbolEqualityComparer.Default.Equals(declaration.Symbol, reference.Local)
                    && declaration.Initializer is not null)
                {
                    writes.Add((declaration.Initializer.Value, _conditional(operation, root)));
                }
                else if (operation is IAssignmentOperation assignment
                    && assignment.Target is ILocalReferenceOperation target
                    && SymbolEqualityComparer.Default.Equals(target.Local, reference.Local))
                {
                    writes.Add((assignment.Value, assignment is ICompoundAssignmentOperation or ICoalesceAssignmentOperation
                        || _conditional(operation, root)));
                }
                else if (operation is ISimpleAssignmentOperation elementAssignment
                    && _isLocalContainer(elementAssignment.Target, reference.Local))
                {
                    writes.Add((elementAssignment.Value, true));
                }
                else if (operation is IInvocationOperation invocation
                    && invocation.TargetMethod.Name is "Add" or "Insert"
                    && invocation.Instance is ILocalReferenceOperation container
                    && SymbolEqualityComparer.Default.Equals(container.Local, reference.Local))
                {
                    foreach (var argument in invocation.Arguments)
                    {
                        writes.Add((argument.Value, true));
                    }
                }
            }

            foreach (var child in operation.ChildOperations)
            {
                pending.Push(child);
            }
        }

        var values = new List<IOperation>();
        foreach (var write in writes.OrderByDescending(static write => write.Value.Syntax.SpanStart))
        {
            values.Add(write.Value);
            if (!write.Conditional)
            {
                break;
            }
        }

        return values;
    }

    private static bool _isLocalContainer(IOperation operation, ILocalSymbol local)
    {
        var instance = operation switch
        {
            IArrayElementReferenceOperation element => element.ArrayReference,
            IPropertyReferenceOperation { Property.IsIndexer: true } indexer => indexer.Instance,
            _ => null,
        };
        return instance is ILocalReferenceOperation reference
            && SymbolEqualityComparer.Default.Equals(reference.Local, local);
    }

    private static bool _sameLoop(IOperation reference, IOperation operation)
    {
        if (operation.Syntax.Span.Contains(reference.Syntax.Span))
        {
            return false;
        }

        for (var parent = reference.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return false;
            }

            if (parent is ILoopOperation && parent.Syntax.Span.Contains(operation.Syntax.Span))
            {
                return true;
            }
        }

        return false;
    }

    private static bool _conditional(IOperation operation, IOperation root)
    {
        for (var parent = operation.Parent; parent is not null && !ReferenceEquals(parent, root); parent = parent.Parent)
        {
            if (parent is IConditionalOperation or ILoopOperation or ISwitchOperation or ITryOperation or ICoalesceOperation)
            {
                return true;
            }
        }

        return false;
    }

    private Source? _type(ITypeSymbol? type, int depth, HashSet<ITypeSymbol> visited)
    {
        if (type is null || !_enter(depth) || !visited.Add(type) || _isSelfProtecting(type))
        {
            return null;
        }

        var source = _classification(type);
        if (source is not null || depth >= _maxTypeDepth || type.SpecialType != SpecialType.None)
        {
            return source;
        }

        if (type is IArrayTypeSymbol array)
        {
            return _type(array.ElementType, depth + 1, visited);
        }

        if (type is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                source = _type(constraint, depth + 1, visited);
                if (source is not null)
                {
                    return source;
                }
            }

            return null;
        }

        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        foreach (var argument in named.TypeArguments)
        {
            source = _type(argument, depth + 1, visited);
            if (source is not null)
            {
                return source;
            }
        }

        var assembly = named.ContainingAssembly?.Identity.Name;
        if (assembly is "mscorlib" or "netstandard"
            || assembly?.StartsWith("System.", StringComparison.Ordinal) == true
            || assembly?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
        {
            return null;
        }

        foreach (var member in named.GetMembers())
        {
            if (!_enter(depth) || member.IsStatic)
            {
                continue;
            }

            var memberType = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol { IsImplicitlyDeclared: false } field => field.Type,
                _ => null,
            };
            if (memberType is null)
            {
                continue;
            }

            source = _classification(member)
                ?? (member is IPropertySymbol classifiedProperty ? _positionalClassification(classifiedProperty) : null)
                ?? _type(memberType, depth + 1, visited);
            if (source is not null)
            {
                return source;
            }
        }

        return _type(named.BaseType, depth + 1, visited);
    }

    private static Source? _positionalClassification(IPropertySymbol property)
    {
        if (!property.ContainingType.IsRecord)
        {
            return null;
        }

        return property.ContainingType.InstanceConstructors
            .SelectMany(static constructor => constructor.Parameters)
            .Where(parameter => parameter.Name == property.Name
                && SymbolEqualityComparer.Default.Equals(parameter.Type, property.Type))
            .Select(_classification)
            .FirstOrDefault(static source => source is not null);
    }

    private static Source? _classification(ISymbol symbol)
    {
        var sensitive = false;
        var pseudonymous = false;
        foreach (var type in symbol.GetAttributes().Select(static attribute => attribute.AttributeClass))
        {
            if (type?.ToDisplayString() == "Ark.Tools.Compliance.PseudonymousAttribute")
            {
                pseudonymous = true;
                continue;
            }

            if (type?.OriginalDefinition.MetadataName == "SensitiveValueObjectAttribute`1"
                && type?.ContainingNamespace.ToDisplayString() == "Ark.Tools.Compliance")
            {
                sensitive = true;
            }

            if (SinkConfiguration._isOrDerivesFrom(type, "Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute"))
            {
                var name = type!.Name;
                return new Source(symbol, name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name);
            }
        }

        if (!pseudonymous && symbol is INamedTypeSymbol named
            && (sensitive || _isSensitiveContract(named) || named.AllInterfaces.Any(_isSensitiveContract)))
        {
            return new Source(symbol, "SensitiveValue");
        }

        return null;
    }

    private static bool _isSensitiveContract(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.MetadataName == "ISensitiveValue`1"
            && type.ContainingNamespace.ToDisplayString() == "Ark.Tools.Compliance";
    }

    internal sealed class Source
    {
        internal Source(ISymbol symbol, string classification)
        {
            _symbol = symbol;
            _classification = classification;
        }

        internal ISymbol _symbol { get; }

        internal string _classification { get; }
    }
}
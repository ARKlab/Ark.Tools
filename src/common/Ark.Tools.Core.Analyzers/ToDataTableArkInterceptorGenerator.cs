// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Tools.Core.Analyzers;

/// <summary>
/// Emits C# 14 compiler interceptors (see
/// <see href="https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md"/>) for
/// compile-time-known calls to <c>Ark.Tools.Core.DataTableExtensions.ToDataTableArk&lt;T&gt;</c>,
/// replacing them with a direct, reflection-free <see cref="System.Data.DataTable"/> construction
/// generated specifically for the closed type <c>T</c> used at each call site.
/// </summary>
/// <remarks>
/// Only calls whose element type <c>T</c> is a compile-time-known, "flat" type (a sealed-shape class
/// deriving directly from <see cref="object"/>, or a struct/record, with only public instance
/// fields/properties) are intercepted. Any call that does not meet these criteria - including calls
/// where <c>T</c> is an open type parameter, the compilation's C# language version does not support
/// interceptors, or the consuming project has not opted the generated namespace into
/// <c>InterceptorsNamespaces</c> - is left completely untouched and safely falls back to the
/// reflection-based implementation in <c>ShredObjectToDataTable&lt;T&gt;</c>.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ToDataTableArkInterceptorGenerator : IIncrementalGenerator
{
    private const string TargetMethodName = "ToDataTableArk";
    private const string TargetContainingType = "Ark.Tools.Core.DataTableExtensions";

    /// <summary>The namespace interceptor methods are emitted into; must be listed in the consuming project's InterceptorsNamespaces.</summary>
    public const string GeneratedNamespace = "Ark.Tools.Core.Generated";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var callSites = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateInvocation(node),
                transform: static (ctx, ct) => Analyze(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        var languageVersionSupported = context.CompilationProvider.Select(static (compilation, _) =>
            compilation is CSharpCompilation csharpCompilation
            && (int)csharpCompilation.LanguageVersion >= 1400);

        var combined = callSites.Combine(languageVersionSupported);

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var (sites, languageOk) = data;
            if (!languageOk || sites.IsDefaultOrEmpty)
                return;

            var source = Emit(sites);
            if (source is not null)
                spc.AddSource("ToDataTableArkInterceptors.g.cs", source);
        });
    }

    private static bool IsCandidateInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var name = GetSimpleName(invocation.Expression);
        return name is not null && name.Identifier.ValueText == TargetMethodName;
    }

    private static SimpleNameSyntax? GetSimpleName(ExpressionSyntax expression) => expression switch
    {
        SimpleNameSyntax simple => simple,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
        _ => null,
    };

    private static CallSiteModel? Analyze(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
            return null;

        var original = (method.ReducedFrom ?? method).OriginalDefinition;
        if (original.Name != TargetMethodName
            || original.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) != TargetContainingType
            || original.TypeParameters.Length != 1
            || original.Parameters.Length != 1
            || method.TypeArguments.Length != 1)
        {
            return null;
        }

        if (method.TypeArguments[0] is not INamedTypeSymbol elementType)
            return null; // T is an open type parameter, array, pointer, etc. - not compile-time-known here.

        var typeModel = BuildTypeModel(elementType);
        if (typeModel is null)
            return null; // T does not meet the "flat, public, instance-only" eligibility rules.

        var location = semanticModel.GetInterceptableLocation(invocation, cancellationToken);
        if (location is null)
            return null;

        return new CallSiteModel(typeModel.Value, location);
    }

    // Determines whether T is eligible for interception and, if so, builds its cached column plan.
    // Mirrors the eligibility rules documented on the generator: a "flat" class (deriving directly
    // from object) or a struct/record, with only public *instance* fields/properties, each type and
    // its containing types accessible from anywhere in the assembly (so the generated code - which
    // lives in an unrelated namespace - can reference it), and every property having an accessible
    // public getter. Anything else is left for the reflection-based fallback to handle.
    private static TypeModel? BuildTypeModel(INamedTypeSymbol type)
    {
        if (type.IsAnonymousType || !IsGloballyAccessible(type))
            return null;

        if (type.TypeKind == TypeKind.Struct)
        {
            if (type.IsPrimitiveScalar())
                return new TypeModel(type.ToFullyQualifiedString(), type.Name, IsReferenceType: false, IsPrimitiveScalar: true, Members: []);
        }
        else if (type.TypeKind == TypeKind.Class)
        {
            if (type.BaseType is not null && type.BaseType.SpecialType != SpecialType.System_Object)
                return null; // Only "flat" classes (no custom inheritance) are supported.
        }
        else
        {
            return null; // Interfaces, enums-as-T, delegates, etc. are not supported; fall back safely.
        }

        var fields = ImmutableArray.CreateBuilder<MemberModel>();
        var properties = ImmutableArray.CreateBuilder<MemberModel>();
        foreach (var member in type.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsImplicitlyDeclared: false } field:
                    if (field.IsStatic || RequiresRuntimeValueConversion(field.Type))
                        return null;
                    fields.Add(BuildMemberModel(field.Name, field.Type));
                    break;
                case IPropertySymbol { DeclaredAccessibility: Accessibility.Public } property:
                    if (property.IsStatic
                        || property.IsIndexer
                        || RequiresRuntimeValueConversion(property.Type)
                        || property.GetMethod is not { DeclaredAccessibility: Accessibility.Public })
                    {
                        return null; // Matches a property reflection would include but GetValue would throw on; stay safe and fall back.
                    }
                    properties.Add(BuildMemberModel(property.Name, property.Type));
                    break;
            }
        }

        return new TypeModel(
            type.ToFullyQualifiedString(),
            type.MetadataName,
            IsReferenceType: type.TypeKind == TypeKind.Class,
            IsPrimitiveScalar: false,
            Members: fields.ToImmutable().AddRange(properties));
    }

    private static bool RequiresRuntimeValueConversion(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Interface;
    }

    private static MemberModel BuildMemberModel(string name, ITypeSymbol declaredType)
    {
        var underlying = UnwrapNullable(declaredType, out var isNullable);
        var conversion = DetermineConversion(underlying);
        var columnType = conversion switch
        {
            ConversionKind.EnumToString => "global::System.String",
            ConversionKind.LocalDateToDateTime or ConversionKind.LocalDateTimeToDateTime or ConversionKind.InstantToDateTime => "global::System.DateTime",
            ConversionKind.OffsetDateTimeToDateTimeOffset or ConversionKind.OffsetDateToDateTimeOffset => "global::System.DateTimeOffset",
            ConversionKind.LocalTimeToTimeSpan => "global::System.TimeSpan",
            _ => underlying.ToFullyQualifiedString(),
        };

        return new MemberModel(name, isNullable, conversion, columnType);
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type, out bool isNullable)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        {
            isNullable = true;
            return named.TypeArguments[0];
        }

        isNullable = false;
        return type;
    }

    private static ConversionKind DetermineConversion(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
            return ConversionKind.EnumToString;

        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) switch
        {
            "global::NodaTime.LocalDate" => ConversionKind.LocalDateToDateTime,
            "global::NodaTime.LocalDateTime" => ConversionKind.LocalDateTimeToDateTime,
            "global::NodaTime.Instant" => ConversionKind.InstantToDateTime,
            "global::NodaTime.OffsetDateTime" => ConversionKind.OffsetDateTimeToDateTimeOffset,
            "global::NodaTime.OffsetDate" => ConversionKind.OffsetDateToDateTimeOffset,
            "global::NodaTime.LocalTime" => ConversionKind.LocalTimeToTimeSpan,
            _ => ConversionKind.Direct,
        };
    }

    private static bool IsGloballyAccessible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
                return false;
        }

        return true;
    }

    private static string? Emit(ImmutableArray<CallSiteModel> callSites)
    {
        // Grouped by the fully-qualified type name (a plain string) rather than by TypeModel itself:
        // ImmutableArray<T> equality is reference-based, so two BuildTypeModel calls for the exact
        // same T (from different call sites) produce structurally-identical but not reference-equal
        // TypeModel values; grouping by name still correctly deduplicates the emitted method per T.
        var groups = callSites
            .GroupBy(static site => site.Type.FullyQualifiedName, StringComparer.Ordinal)
            .ToArray();
        if (groups.Length == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.Append("namespace ").Append(GeneratedNamespace).AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Ark.Tools.Core.Analyzers.ToDataTableArkInterceptorGenerator\", \"1.0.0\")]");
        sb.AppendLine("    file static class ToDataTableArkInterceptors");
        sb.AppendLine("    {");

        var methodIndex = 0;
        foreach (var group in groups)
        {
            EmitInterceptorMethod(sb, group.First().Type, group.ToArray(), methodIndex++);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data) { }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitInterceptorMethod(StringBuilder sb, TypeModel type, CallSiteModel[] sites, int methodIndex)
    {
        foreach (var site in sites)
        {
            sb.Append("        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(")
              .Append(site.Location.Version)
              .Append(", \"")
              .Append(site.Location.Data)
              .AppendLine("\")]");
        }

        var methodName = "ToDataTableArk_" + methodIndex.ToString(CultureInfo.InvariantCulture);
        sb.Append("        internal static global::System.Data.DataTable ").Append(methodName)
          .Append("(this global::System.Collections.Generic.IEnumerable<").Append(type.FullyQualifiedName).AppendLine("> source)");
        sb.AppendLine("        {");

        if (type.IsPrimitiveScalar)
        {
            EmitPrimitiveBody(sb, type);
        }
        else
        {
            EmitObjectBody(sb, type);
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void EmitPrimitiveBody(StringBuilder sb, TypeModel type)
    {
        sb.Append("            var table = new global::System.Data.DataTable(\"").Append(type.SimpleName).AppendLine("\");");
        sb.Append("            table.Columns.Add(\"Value\", typeof(").Append(type.FullyQualifiedName).AppendLine("));");
        sb.AppendLine("            table.BeginLoadData();");
        sb.AppendLine("            using (var e = source.GetEnumerator())");
        sb.AppendLine("            {");
        sb.AppendLine("                var values = new object?[1];");
        sb.AppendLine("                while (e.MoveNext())");
        sb.AppendLine("                {");
        sb.AppendLine("                    values[0] = e.Current;");
        sb.AppendLine("                    table.LoadDataRow(values, true);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            table.EndLoadData();");
        sb.AppendLine("            return table;");
    }

    private static void EmitObjectBody(StringBuilder sb, TypeModel type)
    {
        sb.Append("            var table = new global::System.Data.DataTable(\"").Append(type.SimpleName).AppendLine("\");");
        foreach (var member in type.Members)
        {
            sb.Append("            table.Columns.Add(\"").Append(member.Name).Append("\", typeof(").Append(member.ColumnTypeFullName).AppendLine("));");
        }

        sb.AppendLine("            table.BeginLoadData();");
        sb.AppendLine("            using (var e = source.GetEnumerator())");
        sb.AppendLine("            {");
        sb.AppendLine("                while (e.MoveNext())");
        sb.AppendLine("                {");
        sb.AppendLine("                    var it = e.Current;");

        if (type.IsReferenceType && type.Members.Length > 0)
        {
            sb.AppendLine("                    if (it is null)");
            sb.AppendLine("                        throw new global::System.Reflection.TargetException(\"Non-static method requires a target.\");");
        }

        sb.Append("                    var values = new object?[").Append(type.Members.Length).AppendLine("];");
        for (var i = 0; i < type.Members.Length; i++)
        {
            sb.Append("                    values[").Append(i).Append("] = ").Append(BuildValueExpressionText(type.Members[i])).AppendLine(";");
        }

        sb.AppendLine("                    table.LoadDataRow(values, true);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            table.EndLoadData();");
        sb.AppendLine("            return table;");
    }

    // Builds a C# expression equivalent to the runtime's BuildValueExpression/BuildNonNullableConversion:
    // for a nullable member, only converts/boxes when HasValue (else null); otherwise applies the
    // member's conversion (enum-to-string, NodaTime-to-.NET, or a direct passthrough) unconditionally.
    private static string BuildValueExpressionText(MemberModel member)
    {
        var accessor = "it.@" + member.Name;

        if (member.Conversion == ConversionKind.Direct)
        {
            // Direct passthrough: C#'s own boxing conversion for Nullable<T> already yields null when
            // !HasValue and a boxed T otherwise, exactly matching the reflection fallback's semantics.
            return accessor;
        }

        if (!member.IsNullable)
        {
            return ApplyConversion(accessor, member.Conversion);
        }

        var nonNullAccessor = accessor + ".Value";
        var convertedExpression = ApplyConversion(nonNullAccessor, member.Conversion);
        return accessor + ".HasValue ? (object)(" + convertedExpression + ") : null";
    }

    private static string ApplyConversion(string accessor, ConversionKind conversion) => conversion switch
    {
        ConversionKind.EnumToString => accessor + ".ToString()",
        ConversionKind.LocalDateToDateTime => accessor + ".ToDateTimeUnspecified()",
        ConversionKind.LocalDateTimeToDateTime => accessor + ".ToDateTimeUnspecified()",
        ConversionKind.InstantToDateTime => accessor + ".ToDateTimeUtc()",
        ConversionKind.OffsetDateTimeToDateTimeOffset => accessor + ".ToDateTimeOffset()",
        ConversionKind.OffsetDateToDateTimeOffset => accessor + ".At(global::NodaTime.LocalTime.Midnight).ToDateTimeOffset()",
        ConversionKind.LocalTimeToTimeSpan => "global::System.TimeSpan.FromTicks(" + accessor + ".TickOfDay)",
        _ => accessor,
    };
}

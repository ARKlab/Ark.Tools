// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ark.Tools.Compliance.Generators;

/// <summary>Generates safe APIs for string-backed sensitive value objects.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class SensitiveValueObjectGenerator : IIncrementalGenerator
{
    private const string _attributeName = "Ark.Tools.Compliance.SensitiveValueObjectAttribute`1";

    private static readonly DiagnosticDescriptor _unsupportedType = new(
        "ARKPII201",
        "Unsupported sensitive value type",
        "Sensitive value object '{0}' must use string as its underlying type",
        "Compliance",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII201.md");

    private static readonly DiagnosticDescriptor _invalidDeclaration = new(
        "ARKPII202",
        "Invalid sensitive value object declaration",
        "Sensitive value object '{0}' must be declared as a non-generic readonly partial struct",
        "Compliance",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII202.md");

    private static readonly DiagnosticDescriptor _clearTextToString = new(
        "ARKPII203",
        "Cleartext ToString is not allowed",
        "Sensitive value object '{0}' cannot declare a ToString method",
        "Compliance",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII203.md");

    private static readonly DiagnosticDescriptor _invalidHook = new(
        "ARKPII204",
        "Invalid sensitive value object hook",
        "Sensitive value object '{0}' declares '{1}' with an unsupported signature; it must be 'private static {2} {1}(string value)'",
        "Compliance",
        DiagnosticSeverity.Error,
        true, helpLinkUri: "https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII204.md");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var values = context.SyntaxProvider.ForAttributeWithMetadataName(
                _attributeName,
                static (node, _) => node is StructDeclarationSyntax,
                static (ctx, token) => _parse(ctx, token))
            .Where(static value => value is not null);

        context.RegisterSourceOutput(values, static (spc, value) =>
        {
            var model = value!.Value;
            if (model.Diagnostic is not null)
            {
                spc.ReportDiagnostic(model.Diagnostic.Value.Create());
                return;
            }

            spc.AddSource(model.HintName!, _emit(model));
        });
    }

    private static Model? _parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TargetNode is not StructDeclarationSyntax declaration
            || context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var attribute = context.Attributes[0];

        var location = declaration.GetLocation();
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var namespaceName = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();
        var isPublic = type.DeclaredAccessibility == Accessibility.Public;

        if (attribute.AttributeClass is not INamedTypeSymbol attributeClass
            || attributeClass.TypeArguments.Length != 1
            || attributeClass.TypeArguments[0].SpecialType != SpecialType.System_String)
        {
            return new Model(null, null, null, false, default,
                new DiagnosticSpec(DiagnosticKind.UnsupportedType, typeName, null, null, SourceLocation.Create(location)));
        }

        if (type.ContainingType is not null
            || type.Arity != 0
            || !declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
            || !declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)))
        {
            return new Model(null, null, null, false, default,
                new DiagnosticSpec(DiagnosticKind.InvalidDeclaration, typeName, null, null, SourceLocation.Create(location)));
        }

        if (type.GetMembers("ToString").OfType<IMethodSymbol>().Any(static method =>
                method.Parameters.Length == 0))
        {
            return new Model(null, null, null, false, default,
                new DiagnosticSpec(DiagnosticKind.ClearTextToString, typeName, null, null, SourceLocation.Create(location)));
        }

        var validateHook = _findHook(type, "_validate", "Ark.Tools.Compliance.ValidationResult", out var invalidValidate);
        if (invalidValidate)
        {
            return new Model(null, null, null, false, default,
                new DiagnosticSpec(DiagnosticKind.InvalidHook, typeName, "_validate",
                    "global::Ark.Tools.Compliance.ValidationResult", SourceLocation.Create(location)));
        }

        var normalizeHook = _findHook(type, "_normalize", "string", out var invalidNormalize);
        if (invalidNormalize)
        {
            return new Model(null, null, null, false, default,
                new DiagnosticSpec(DiagnosticKind.InvalidHook, typeName, "_normalize", "string", SourceLocation.Create(location)));
        }

        var redaction = _getEnumValue(attribute, 0, "Redaction", 0);
        return new Model(
            _hintName(namespaceName, typeName),
            namespaceName,
            typeName,
            isPublic,
            new Settings(redaction, validateHook, normalizeHook),
            null);
    }

    private static bool _findHook(INamedTypeSymbol type, string name, string returnType, out bool invalid)
    {
        var candidates = type.GetMembers(name).OfType<IMethodSymbol>().ToArray();
        var valid = candidates.Any(method => _isStringHook(method, returnType));
        invalid = !valid && candidates.Length > 0;
        return valid;
    }

    private static bool _isStringHook(IMethodSymbol method, string returnType)
    {
        return method.IsStatic
            && method.DeclaredAccessibility == Accessibility.Private
            && method.Parameters.Length == 1
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && (returnType == "string"
                ? method.ReturnType.SpecialType == SpecialType.System_String
                : method.ReturnType.ToDisplayString() == returnType);
    }

    private static string _hintName(string? namespaceName, string typeName)
    {
        return (namespaceName is null
            ? typeName
            : namespaceName + "." + typeName)
            + ".SensitiveValueObject.g.cs";
    }

    private static int _getEnumValue(AttributeData attribute, int argumentIndex, string name, int defaultValue)
    {
        if (attribute.ConstructorArguments.Length > argumentIndex
            && attribute.ConstructorArguments[argumentIndex].Value is int constructorValue)
        {
            return constructorValue;
        }

        var namedValue = attribute.NamedArguments
            .Where(namedArgument => namedArgument.Key == name)
            .Select(namedArgument => namedArgument.Value.Value is int value ? value : (int?)null)
            .FirstOrDefault();
        return namedValue ?? defaultValue;
    }

    private static string _emit(Model model)
    {
        var namespaceName = model.NamespaceName;
        var typeName = model.TypeName!;
        var accessibility = model.IsPublic ? "public " : "internal ";
        var settings = model.Settings;
        var redactor = settings.Redaction switch
        {
            1 => "global::Ark.Tools.Compliance.ArkMaskingRedactor.Instance",
            2 => "new global::Ark.Tools.Compliance.ArkHmacRedactor(global::System.Environment.GetEnvironmentVariable(\"ARK_TOOLS_COMPLIANCE_HMAC_KEY\"))",
            3 => "global::Ark.Tools.Compliance.ArkNullRedactor.Instance",
            _ => "global::Ark.Tools.Compliance.ArkErasingRedactor.Instance",
        };

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        if (namespaceName is not null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine(";");
        }

        builder.Append("[global::System.Text.Json.Serialization.JsonConverter(typeof(global::Ark.Tools.Compliance.SensitiveValueJsonConverter<")
            .Append(typeName).AppendLine(">))]");
        builder.Append("[global::System.Diagnostics.DebuggerDisplay(\"{ToString(),nq}\")]").AppendLine();
        builder.Append("[global::System.ComponentModel.TypeConverter(typeof(global::Ark.Tools.Compliance.SensitiveValueTypeConverter<")
            .Append(typeName).AppendLine(">))]");
        builder.Append(accessibility).Append("readonly partial struct ").Append(typeName)
            .Append(" : global::System.IEquatable<").Append(typeName)
            .Append(">, global::System.IFormattable, global::System.ISpanFormattable, global::Ark.Tools.Compliance.ISensitiveValue<")
            .Append(typeName).AppendLine(">");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly string _value;");
        builder.Append("    private ").Append(typeName).AppendLine("(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        _value = value;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>Creates a validated sensitive value object.</summary>");
        builder.AppendLine("    /// <param name=\"value\">The cleartext value.</param>");
        builder.AppendLine("    /// <returns>A normalized value object.</returns>");
        builder.Append("    public static ").Append(typeName).AppendLine(" From(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (value is null)");
        builder.AppendLine("            throw new global::System.ArgumentNullException(nameof(value));");
        builder.AppendLine("        var normalized = _normalize(value);");
        builder.AppendLine("        var validation = _validate(normalized);");
        builder.AppendLine("        if (!validation.IsValid)");
        builder.AppendLine("            throw new global::System.ArgumentException(validation.ErrorMessage ?? \"The value is invalid.\", nameof(value));");
        builder.Append("        return new ").Append(typeName).AppendLine("(normalized);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>Tries to create a validated sensitive value object.</summary>");
        builder.AppendLine("    /// <param name=\"value\">The cleartext value.</param>");
        builder.AppendLine("    /// <param name=\"result\">The normalized value object when valid.</param>");
        builder.AppendLine("    /// <returns><see langword=\"true\"/> when the value is valid.</returns>");
        builder.Append("    public static bool TryFrom(string? value, out ").Append(typeName).AppendLine(" result)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (value is null)");
        builder.AppendLine("        {");
        builder.Append("            result = default(").Append(typeName).AppendLine(");");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine("        var normalized = _normalize(value);");
        builder.AppendLine("        var validation = _validate(normalized);");
        builder.AppendLine("        if (!validation.IsValid)");
        builder.AppendLine("        {");
        builder.Append("            result = default(").Append(typeName).AppendLine(");");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.Append("        result = new ").Append(typeName).AppendLine("(normalized);");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>Reveals the cleartext value for an explicitly named and categorized purpose.</summary>");
        builder.AppendLine("    /// <param name=\"purpose\">The reviewed compliance purpose, carrying its processing category.</param>");
        builder.AppendLine("    /// <returns>The cleartext value.</returns>");
        builder.AppendLine("    public string Reveal(global::Ark.Tools.Compliance.CompliancePurpose purpose)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (string.IsNullOrWhiteSpace(purpose.Reason))");
        builder.AppendLine("            throw new global::System.ArgumentException(\"A compliance purpose is required.\", nameof(purpose));");
        builder.AppendLine("        if (purpose.Category == global::Ark.Tools.Compliance.CompliancePurposeCategory.Unspecified)");
        builder.AppendLine("            throw new global::System.ArgumentException(\"A categorized compliance purpose is required.\", nameof(purpose));");
        builder.AppendLine("        return _value ?? string.Empty;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <inheritdoc />");
        builder.AppendLine("    public override string ToString() => _redact(_value ?? string.Empty);");
        builder.AppendLine();
        builder.AppendLine("    /// <inheritdoc />");
        builder.AppendLine("    public string ToString(string? format, global::System.IFormatProvider? formatProvider) => ToString();");
        builder.AppendLine();
        builder.AppendLine("    /// <inheritdoc />");
        builder.AppendLine("    public bool TryFormat(global::System.Span<char> destination, out int charsWritten, global::System.ReadOnlySpan<char> format, global::System.IFormatProvider? provider)");
        builder.AppendLine("    {");
        builder.AppendLine("        var redacted = ToString();");
        builder.AppendLine("        if (redacted.AsSpan().Length > destination.Length)");
        builder.AppendLine("        {");
        builder.AppendLine("            charsWritten = 0;");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine("        redacted.AsSpan().CopyTo(destination);");
        builder.AppendLine("        charsWritten = redacted.Length;");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <inheritdoc />");
        builder.Append("    public bool Equals(").Append(typeName).AppendLine(" other) => string.Equals(_value, other._value, global::System.StringComparison.Ordinal);");
        builder.AppendLine();
        builder.AppendLine("    /// <inheritdoc />");
        builder.AppendLine("    public override bool Equals(object? obj) => obj is " + typeName + " other && Equals(other);");
        builder.AppendLine();
        builder.AppendLine("    /// <inheritdoc />");
        builder.AppendLine("    public override int GetHashCode() => _value?.GetHashCode(global::System.StringComparison.Ordinal) ?? 0;");
        builder.AppendLine();
        builder.Append("    public static bool operator ==(").Append(typeName).Append(" left, ").Append(typeName).AppendLine(" right) => left.Equals(right);");
        builder.Append("    public static bool operator !=(").Append(typeName).Append(" left, ").Append(typeName).AppendLine(" right) => !left.Equals(right);");
        builder.AppendLine();
        builder.Append("    private static readonly global::Microsoft.Extensions.Compliance.Redaction.Redactor _redactor = ")
            .Append(redactor).AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("    private static string _redact(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        var buffer = new char[_redactor.GetRedactedLength(value.AsSpan())];");
        builder.AppendLine("        var length = _redactor.Redact(value.AsSpan(), buffer);");
        builder.AppendLine("        return new string(buffer, 0, length);");
        builder.AppendLine("    }");
        builder.AppendLine();
        if (!settings.HasValidate)
            builder.AppendLine("    private static global::Ark.Tools.Compliance.ValidationResult _validate(string value) => global::Ark.Tools.Compliance.ValidationResult.Ok;");
        if (!settings.HasNormalize)
            builder.AppendLine("    private static string _normalize(string value) => value;");

        builder.AppendLine("}");
        return builder.ToString();
    }

    private readonly record struct Model(
        string? HintName,
        string? NamespaceName,
        string? TypeName,
        bool IsPublic,
        Settings Settings,
        DiagnosticSpec? Diagnostic);

    private readonly record struct Settings(
        int Redaction,
        bool HasValidate,
        bool HasNormalize);

    private enum DiagnosticKind
    {
        UnsupportedType,
        InvalidDeclaration,
        ClearTextToString,
        InvalidHook,
    }

    private readonly record struct DiagnosticSpec(
        DiagnosticKind Kind,
        string TypeName,
        string? HookName,
        string? ReturnType,
        SourceLocation Location)
    {
        internal Diagnostic Create()
        {
            return Kind switch
            {
                DiagnosticKind.UnsupportedType => Diagnostic.Create(_unsupportedType, Location.ToLocation(), TypeName),
                DiagnosticKind.InvalidDeclaration => Diagnostic.Create(_invalidDeclaration, Location.ToLocation(), TypeName),
                DiagnosticKind.ClearTextToString => Diagnostic.Create(_clearTextToString, Location.ToLocation(), TypeName),
                DiagnosticKind.InvalidHook => Diagnostic.Create(
                    _invalidHook,
                    Location.ToLocation(),
                    TypeName,
                    HookName,
                    ReturnType),
                _ => throw new InvalidOperationException(),
            };
        }
    }

    private readonly record struct SourceLocation(
        string FilePath,
        int Start,
        int Length,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter)
    {
        internal static SourceLocation Create(Location location)
        {
            if (!location.IsInSource)
            {
                return new SourceLocation(string.Empty, 0, 0, 0, 0, 0, 0);
            }

            var lineSpan = location.GetLineSpan().Span;
            return new SourceLocation(
                location.SourceTree?.FilePath ?? string.Empty,
                location.SourceSpan.Start,
                location.SourceSpan.Length,
                lineSpan.Start.Line,
                lineSpan.Start.Character,
                lineSpan.End.Line,
                lineSpan.End.Character);
        }

        internal Location ToLocation()
        {
            return string.IsNullOrEmpty(FilePath)
                ? Location.None
                : Location.Create(
                    FilePath,
                    new TextSpan(Start, Length),
                    new LinePositionSpan(
                        new LinePosition(StartLine, StartCharacter),
                        new LinePosition(EndLine, EndCharacter)));
        }
    }
}

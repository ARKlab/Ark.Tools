// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Generators;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>The generators' allocation-free attribute name match must agree with the display-string comparison it replaced.</summary>
[TestClass]
public sealed class GeneratorSymbolNamesTests
{
    /// <summary>Every non-generic type compared with every name gives the same answer as <c>ToDisplayString() == name</c>.</summary>
    [TestMethod]
    public void IsFullNameMatchesDisplayStringComparison()
    {
        var compilation = CSharpCompilation.Create("SymbolNames",
            [CSharpSyntaxTree.ParseText("""
                public class Global { }
                namespace N1.N2 { public class A { } }
                namespace N1.N2X { public class A { } }
                namespace X.N1.N2 { public class A { } }
                namespace N1 { public class Outer { public class Inner { } } public class Gen<T> { } public class N2A { } }
                class UsesMissing : MissingType { }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var types = compilation.GetSymbolsWithName(static _ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>()
            .Append(compilation.GetTypeByMetadataName("UsesMissing")!.BaseType!) // an error type
            .ToArray();
        string[] names =
        [
            "Global", ".Global", "N1.N2.A", "N2.A", "1.N2.A", "N1.N2X.A", "X.N1.N2.A", "N1.Outer.Inner", "N1.Outer",
            "N1.Gen<T>", "N1.Gen", "N1.N2A", "N1N2.A", "MissingType", "A", "",
        ];
        var isFullName = typeof(MessagingNetworkGenerator).Assembly
            .GetType("Ark.Tools.MediatorFramework.Generators.SymbolNames")!
            .GetMethod("_isFullName")!
            .CreateDelegate<Func<INamedTypeSymbol?, string, bool>>();

        types.Should().HaveCountGreaterThan(8);
        foreach (var type in types)
        {
            foreach (var name in names)
            {
                // Generic types are out of contract: their display string carries the type parameters.
                var expected = type.Arity == 0 && type.ToDisplayString() == name;
                isFullName(type, name).Should().Be(expected, "{0} compared with '{1}'", type.ToDisplayString(), name);
            }
        }

        isFullName(null, "N1.N2.A").Should().BeFalse();
    }

    /// <summary>Every namespace compared with every name gives the same answer as <c>ToDisplayString() == name</c>.</summary>
    [TestMethod]
    public void IsNamespaceMatchesDisplayStringComparison()
    {
        var compilation = CSharpCompilation.Create("SymbolNames",
            [CSharpSyntaxTree.ParseText("""
                namespace N1.N2 { public class A { } }
                namespace N1.N2X { public class A { } }
                namespace X.N1.N2 { public class A { } }
                namespace System.Collections.Generic.Custom { public class A { } }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var namespaces = compilation.GetSymbolsWithName(static _ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>()
            .Select(static type => type.ContainingNamespace)
            .Append(compilation.GlobalNamespace)
            .Append(compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!.ContainingNamespace)
            .ToArray();
        string[] names =
        [
            "N1.N2", "N2", "1.N2", "N1", "N1.N2X", "X.N1.N2", "N1.N2.", ".N1.N2", "System.Collections.Generic",
            "System.Collections", "Collections.Generic", "System.Collections.Generic.Custom", "<global namespace>", "",
        ];
        var isNamespace = _symbolNames("_isNamespace").CreateDelegate<Func<INamespaceSymbol?, string, bool>>();

        foreach (var @namespace in namespaces)
        {
            foreach (var name in names)
            {
                // The global namespace is out of contract: its display string is not a name.
                var expected = !@namespace.IsGlobalNamespace && @namespace.ToDisplayString() == name;
                isNamespace(@namespace, name).Should().Be(expected, "{0} compared with '{1}'", @namespace.ToDisplayString(), name);
            }
        }

        isNamespace(null, "N1").Should().BeFalse();
    }

    /// <summary>
    /// A generic definition matches by metadata name as its display string comparison did, and a nested type or one in
    /// another namespace does not.
    /// </summary>
    [TestMethod]
    public void IsMetadataNameMatchesGenericDefinitionDisplayString()
    {
        var compilation = CSharpCompilation.Create("SymbolNames",
            [CSharpSyntaxTree.ParseText("""
                namespace System.Collections.Generic.Custom { public interface IEnumerable<T> { } }
                namespace N1 { public class Outer { public interface IEnumerable<T> { } } public class Plain { } }
                public class Global<T> { }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var types = new[]
        {
            compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")!,
            compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!,
            compilation.GetTypeByMetadataName("System.IParsable`1")!,
            compilation.GetTypeByMetadataName("System.Collections.Generic.Custom.IEnumerable`1")!,
            compilation.GetTypeByMetadataName("N1.Outer+IEnumerable`1")!,
            compilation.GetTypeByMetadataName("N1.Plain")!,
            compilation.GetTypeByMetadataName("Global`1")!,
        };
        (string Namespace, string MetadataName, string Display)[] names =
        [
            ("System.Collections.Generic", "IEnumerable`1", "System.Collections.Generic.IEnumerable<T>"),
            ("System.Collections.Generic", "List`1", "System.Collections.Generic.List<T>"),
            ("System", "IParsable`1", "System.IParsable<TSelf>"),
            ("N1", "IEnumerable`1", "N1.IEnumerable<T>"),
            ("N1", "Plain", "N1.Plain"),
            ("System.Collections.Generic", "IEnumerable", "System.Collections.Generic.IEnumerable"),
        ];
        var isMetadataName = _symbolNames("_isMetadataName").CreateDelegate<Func<INamedTypeSymbol?, string, string, bool>>();

        foreach (var type in types)
        {
            foreach (var (@namespace, metadataName, display) in names)
            {
                isMetadataName(type, @namespace, metadataName).Should().Be(
                    type.ToDisplayString() == display, "{0} compared with '{1}'", type.ToDisplayString(), display);
            }
        }

        isMetadataName(null, "System", "IParsable`1").Should().BeFalse();
    }

    private static System.Reflection.MethodInfo _symbolNames(string name)
        => typeof(MessagingNetworkGenerator).Assembly
            .GetType("Ark.Tools.MediatorFramework.Generators.SymbolNames")!
            .GetMethod(name)!;
}

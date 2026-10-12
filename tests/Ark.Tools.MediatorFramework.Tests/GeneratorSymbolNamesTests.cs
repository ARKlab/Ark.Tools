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
        var types = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>()
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
}

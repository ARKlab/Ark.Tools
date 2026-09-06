// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ark.Tools.Compliance.Sql.Tests;

internal static class SqlTestCompilation
{
    private static readonly MetadataReference[] _references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(static path => MetadataReference.CreateFromFile(path))
        .ToArray();

    internal static CSharpCompilation _create(string source)
    {
        return CSharpCompilation.Create("SqlPolicyTests",
            [CSharpSyntaxTree.ParseText("using System;\nusing Ark.Tools.Compliance;\nusing Ark.Tools.Compliance.Sql;\n" + source)],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}

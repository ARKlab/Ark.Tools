// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ark.Tools.Compliance.Sql.Tests;

internal sealed class SqlTestOptionsProvider(bool enabled) : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _options = new Options(enabled);

    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions => _options;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return _options;
    }

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return _options;
    }

    private sealed class Options(bool enabled) : AnalyzerConfigOptions
    {
        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value)
        {
            value = enabled ? "true" : "false";
            return key == "build_property.EnableArkToolsCompliance";
        }
    }
}

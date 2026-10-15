// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Build.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ark.Tools.Compliance.Sql.Tasks;

/// <summary>Writes the compliance SQL templates of the generated manifests, replacing the given SQLCMD tokens.</summary>
public sealed class ArkWriteComplianceSqlTemplates : ITask
{
    private const string _marker = "// ArkComplianceSqlTemplate:";
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public IBuildEngine? BuildEngine { get; set; }

    /// <inheritdoc />
    public ITaskHost? HostObject { get; set; }

    /// <summary>The generated manifests that hold the base64 encoded templates.</summary>
    [Required]
    public ITaskItem[] GeneratedFiles { get; set; } = [];

    /// <summary>The SQLCMD variables to replace: the item is the name, its <c>Value</c> metadata the replacement.</summary>
    public ITaskItem[]? Tokens { get; set; }

    /// <summary>The folder that receives the rendered scripts.</summary>
    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>The rendered scripts.</summary>
    [Output]
    public string[] OutputFiles { get; set; } = [];

    /// <inheritdoc />
    public bool Execute()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in Tokens ?? [])
        {
            var name = token.ItemSpec;
            var value = token.GetMetadata("Value");
            if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.None, _regexTimeout) || value.IndexOf("$(", StringComparison.Ordinal) >= 0
                || Regex.IsMatch(value, @"[\x00-\x1F\x7F]", RegexOptions.None, _regexTimeout) || values.ContainsKey(name))
            {
                _logError("Invalid or duplicate ArkComplianceSqlToken '" + name + "'. Values must be single-line and cannot introduce SQLCMD variables.");
                return false;
            }
            values.Add(name, value);
        }
        var outputs = new List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in GeneratedFiles)
        {
            foreach (var line in File.ReadLines(file.ItemSpec).Where(static l => l.StartsWith(_marker, StringComparison.Ordinal)))
            {
                var start = _marker.Length;
                var separator = line.IndexOf(':', start);
                if (separator <= start || separator == line.Length - 1)
                {
                    _logError("Invalid compliance SQL template marker.");
                    return false;
                }
                var name = line.Substring(start, separator - start);
                if (!Regex.IsMatch(name, @"^policy-[a-f0-9]{64}\.compliance\.sql$", RegexOptions.None, _regexTimeout))
                {
                    _logError("Invalid compliance SQL artifact name.");
                    return false;
                }
                var encoded = line.Substring(separator + 1).Trim();
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(encoded);
                }
                catch (FormatException)
                {
                    _logError("Invalid base64 payload in compliance SQL template '" + name + "'.");
                    return false;
                }
                var template = Encoding.UTF8.GetString(bytes);
                // Tokens occur only inside bracketed identifiers or string literals. Escape each replacement
                // for its own SQL context, never recursively expanding replacement values.
                var sql = Regex.Replace(template, @"\[(?:[^\]]|\]\])*\]|'(?:[^']|'')*'", match =>
                {
                    var identifier = match.Value[0] == '[';
                    return Regex.Replace(match.Value, @"\$\((?<name>[A-Za-z_][A-Za-z0-9_]*)\)", variable =>
                    {
                        if (!values.TryGetValue(variable.Groups["name"].Value, out var replacement)) return variable.Value;
                        return identifier ? replacement.Replace("]", "]]") : replacement.Replace("'", "''");
                    }, RegexOptions.None, _regexTimeout);
                }, RegexOptions.None, _regexTimeout);
                Directory.CreateDirectory(OutputDirectory);
                var output = Path.Combine(OutputDirectory, name);
                if (!File.Exists(output) || !string.Equals(File.ReadAllText(output), sql, StringComparison.Ordinal))
                    File.WriteAllText(output, sql, new UTF8Encoding(false));
                expected.Add(Path.GetFullPath(output));
                outputs.Add(output);
            }
        }
        // A removed opt-in must not leave a stale deployable policy behind.
        if (Directory.Exists(OutputDirectory))
        {
            foreach (var old in Directory.GetFiles(OutputDirectory, "policy-*.compliance.sql").Where(f => !expected.Contains(Path.GetFullPath(f))))
                File.Delete(old);
        }
        OutputFiles = outputs.ToArray();
        return true;
    }

    private void _logError(string message)
    {
        BuildEngine?.LogErrorEvent(new BuildErrorEventArgs(null, null, BuildEngine.ProjectFileOfTaskNode,
            BuildEngine.LineNumberOfTaskNode, BuildEngine.ColumnNumberOfTaskNode, 0, 0, message, null,
            nameof(ArkWriteComplianceSqlTemplates)));
    }
}

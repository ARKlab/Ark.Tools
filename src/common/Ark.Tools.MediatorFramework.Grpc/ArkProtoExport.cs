// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>Exports source-generated and shared protobuf assets for polyglot clients.</summary>
public static class ArkProtoExport
{
    /// <summary>Handles the proto export command-line invocation.</summary>
    /// <param name="args">The process arguments.</param>
    /// <returns><see langword="true"/> when the invocation was handled.</returns>
    [SuppressMessage("Usage", "MA0045", Justification = "The export command is synchronous by design.")]
    [SuppressMessage("Trimming", "IL2026", Justification = "Export runs before the application starts and uses the generated type by its stable name.")]
    [SuppressMessage("Trimming", "IL2075", Justification = "Export runs before the application starts and uses generated public members.")]
    public static bool TryHandle(string[] args)
    {
        var index = Array.IndexOf(args, "--ark-export-proto");
        if (index < 0 || index + 1 >= args.Length)
            return false;

        var directory = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(directory);

        var generatedType = Assembly.GetEntryAssembly()?.GetType(
            "Ark.MediatorFramework.Generated.ArkGeneratedProtos");
        var getFiles = generatedType?.GetMethod("GetFiles", BindingFlags.Public | BindingFlags.Static);
        if (getFiles?.Invoke(null, null) is Array files)
        {
            foreach (var file in files)
            {
                var fileName = (string)file.GetType().GetField("Item1")!.GetValue(file)!;
                if (Path.IsPathRooted(fileName))
                    throw new InvalidOperationException($"Generated protobuf asset path '{fileName}' must be relative.");
                var content = (string)file.GetType().GetField("Item2")!.GetValue(file)!;
                File.WriteAllText(Path.Join(directory, fileName), content);
            }
        }

        WriteSharedAsset("ark/nodatime.proto", directory, Assembly.Load("Ark.Tools.Nodatime.Protobuf"));
        WriteSharedAsset("ark/mediator.proto", directory, typeof(ArkProtoExport).Assembly);
        return true;
    }

    [SuppressMessage("Usage", "MA0045", Justification = "The export command is synchronous by design.")]
    private static void WriteSharedAsset(string relativePath, string directory, Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(relativePath.Replace('/', '.'), StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");
        var relativeOutputPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relativeOutputPath))
            throw new ArgumentException("Path must be relative.", nameof(relativePath));

        var output = Path.Join(directory, relativeOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var file = File.Create(output);
        stream.CopyTo(file);
    }
}

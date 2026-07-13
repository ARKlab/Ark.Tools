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
                var content = (string)file.GetType().GetField("Item2")!.GetValue(file)!;
                File.WriteAllText(Path.Combine(directory, fileName), content);
            }
        }

        WriteSharedAsset("ark/nodatime.proto", directory, Assembly.Load("Ark.Tools.Nodatime.Protobuf"));
        WriteSharedAsset("ark/mediator.proto", directory, typeof(ArkProtoExport).Assembly);
        return true;
    }

    private static void WriteSharedAsset(string relativePath, string directory, Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(relativePath.Replace('/', '.'), StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded protobuf asset '{relativePath}' was not found.");
        var output = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var file = File.Create(output);
        stream.CopyTo(file);
    }
}

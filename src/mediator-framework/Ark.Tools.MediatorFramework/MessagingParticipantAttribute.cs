// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares how a participant joins a transport-neutral messaging network.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingParticipantAttribute : Attribute
{
    /// <summary>The suffix stripped from a participant class name before identity normalization.</summary>
    public const string IdentityClassSuffix = "Participant";

    /// <summary>Gets or sets the participant identity.</summary>
    public string? Identity { get; set; }

    /// <summary>Gets or sets messages processed by this participant.</summary>
    public Type[] Processes { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets events published by this participant.</summary>
    public Type[] Publishes { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets events subscribed to by this participant.</summary>
    public Type[] Subscribes { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets serialization protocols supported by this participant.</summary>
    public SerializationProtocol[] Serializers { get; set; } = Array.Empty<SerializationProtocol>();

    /// <summary>Gets or sets the participant's write protocol.</summary>
    public SerializationProtocol DefaultSerializer { get; set; }

    /// <summary>Gets or sets the retry policy type.</summary>
    public Type? Retry { get; set; }

    /// <summary>Gets or sets the sender-side compression algorithm.</summary>
    public CompressionAlgorithm Compression { get; set; }

    /// <summary>Gets or sets the minimum payload size for compression.</summary>
    public int CompressionMinimumSizeBytes { get; set; }

    /// <summary>
    /// Normalizes a participant class name into its portable identity: a trailing
    /// <see cref="IdentityClassSuffix"/> is removed and word boundaries become lowercase
    /// hyphen-separated segments (<c>PrintingFunctionsParticipant</c> becomes
    /// <c>printing-functions</c>). Generated participant partial classes expose the
    /// result as a compile-time identity constant.
    /// </summary>
    /// <param name="className">The participant class name.</param>
    /// <returns>The normalized participant identity.</returns>
    public static string NormalizeIdentity(string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(className);

        var value = className.EndsWith(IdentityClassSuffix, StringComparison.Ordinal)
            ? className[..^IdentityClassSuffix.Length]
            : className;
        return string.Join("-", _words(value).Select(word => word.ToLowerInvariant()));
    }

    private static IEnumerable<string> _words(string value)
    {
        var word = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var startsWord = index > 0
                && char.IsUpper(character)
                && (char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1])));
            if (startsWord && word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
            if (char.IsLetterOrDigit(character))
                word.Append(character);
            else if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }
        if (word.Length > 0)
            yield return word.ToString();
    }
}

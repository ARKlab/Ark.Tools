// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.Compliance.MessagePack;

using MessagePack;
using MessagePack.Resolvers;

using Vogen;

namespace Ark.MediatorFramework.Sample.Core.API;

/// <summary>Generates the MessagePack formatters of the value objects carried by MessagePack contracts.</summary>
[MessagePack<AuditId>]
[MessagePack<BookTitle>]
[MessagePack<EditionFormat>]
[MessagePack<PageCount>]
[MessagePack<FileSize>]
public sealed partial class SampleMessagePackFormatters
{
    /// <summary>
    /// Gets the resolver for the value objects of the MessagePack contracts: the Vogen formatters generated above and
    /// the compliance sensitive value objects (such as <see cref="PersonName"/>). Hosts compose
    /// it ahead of their other resolvers.
    /// </summary>
    public static IFormatterResolver Resolver { get; } = _createResolver();

    private static IFormatterResolver _createResolver()
    {
        // The compliance resolver serves only registered types, and MessagePack caches lookups: register first.
        SensitiveValueFormatterResolver.Register<PersonName>();
        return CompositeResolver.Create(MessagePackFormatters, [SensitiveValueFormatterResolver.Instance]);
    }
}

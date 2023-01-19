// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;
using System.Collections.Generic;

namespace Ark.Tools.ResourceWatcher
{
    public interface IResourceMetadata
    {
        /// <summary>
        /// The "key" identifier of the resource
        /// </summary>
        string ResourceId { get; }
        /// <summary>
        /// The "version" of the resource. Used to avoid retrival of the resource in case if the same version is already been processed successfully
        /// This field is alternative to ModifiedSources field
        /// </summary>
        LocalDateTime Modified { get; }
        /// <summary>
        /// The "versions" of the resource. Used to manage multiple sources for the resource.
        /// The resource will be processed when a new source will be add or at least one source have an updated modified.
        /// </summary>
        /// <remarks>
        /// The keys are lowercase
        /// </remarks>
#if (NET472 || NETSTANDARD2_0)
        Dictionary<string, LocalDateTime>? ModifiedSources { get; }
#else
        Dictionary<string, LocalDateTime>? ModifiedSources { get => null; }
#endif
        /// <summary>
        /// Additional info serialized to the State tracking
        /// </summary>
        object? Extensions { get; }
    }
}

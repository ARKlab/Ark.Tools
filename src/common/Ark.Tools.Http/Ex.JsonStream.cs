// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Flurl;

using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;

namespace Ark.Tools.Http;

public static partial class Ex
{
    /// <summary>
    /// Sends a POST request whose JSON body is serialized straight into the request stream.
    /// </summary>
    /// <remarks>
    /// Unlike <c>PostJsonAsync</c>, which builds the whole body as a string first, this avoids holding a full copy of a large payload in memory.
    /// The Flurl client's <c>JsonSerializer</c> is not used: <paramref name="jsonTypeInfo"/> controls serialization, which keeps the call trimming and AOT safe.
    /// </remarks>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="request">The Flurl request.</param>
    /// <param name="value">The value to send as JSON.</param>
    /// <param name="jsonTypeInfo">The metadata used to serialize <paramref name="value"/>.</param>
    /// <param name="completionOption">When the returned task completes.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The response.</returns>
    public static async Task<IFlurlResponse> PostJsonStreamAsync<T>(this IFlurlRequest request, T value, JsonTypeInfo<T> jsonTypeInfo, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

#pragma warning disable CA2000 // Dispose objects before losing scope | Disposed by SendAsync()
        var content = JsonContent.Create(value, jsonTypeInfo);
#pragma warning restore CA2000 // Dispose objects before losing scope
        return await request.SendAsync(HttpMethod.Post, content, completionOption, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request to <paramref name="url"/> whose JSON body is serialized straight into the request stream.
    /// </summary>
    /// <remarks>See <see cref="PostJsonStreamAsync{T}(IFlurlRequest, T, JsonTypeInfo{T}, HttpCompletionOption, CancellationToken)"/>.</remarks>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="url">The request URL.</param>
    /// <param name="value">The value to send as JSON.</param>
    /// <param name="jsonTypeInfo">The metadata used to serialize <paramref name="value"/>.</param>
    /// <param name="completionOption">When the returned task completes.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The response.</returns>
    public static async Task<IFlurlResponse> PostJsonStreamAsync<T>(this Url url, T value, JsonTypeInfo<T> jsonTypeInfo, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        return await new FlurlRequest(url).PostJsonStreamAsync(value, jsonTypeInfo, completionOption, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request to <paramref name="url"/> whose JSON body is serialized straight into the request stream.
    /// </summary>
    /// <remarks>See <see cref="PostJsonStreamAsync{T}(IFlurlRequest, T, JsonTypeInfo{T}, HttpCompletionOption, CancellationToken)"/>.</remarks>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="url">The request URL.</param>
    /// <param name="value">The value to send as JSON.</param>
    /// <param name="jsonTypeInfo">The metadata used to serialize <paramref name="value"/>.</param>
    /// <param name="completionOption">When the returned task completes.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The response.</returns>
    public static async Task<IFlurlResponse> PostJsonStreamAsync<T>(this string url, T value, JsonTypeInfo<T> jsonTypeInfo, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        return await new FlurlRequest(url).PostJsonStreamAsync(value, jsonTypeInfo, completionOption, cancellationToken).ConfigureAwait(false);
    }
}

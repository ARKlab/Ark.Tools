// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
namespace Ark.ResourceWatcher.Sample.Transform;

/// <summary>
/// Generic interface for transforming input data to output format.
/// </summary>
/// <typeparam name="TInput">The input type (bytes, objects, or parsed types).</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
public interface ITransformService<TInput, TOutput>
{
    /// <summary>
    /// Transforms the input data to the output format.
    /// </summary>
    /// <param name="input">The input data to transform.</param>
    /// <returns>The transformed output.</returns>
    TOutput Transform(TInput input);
}

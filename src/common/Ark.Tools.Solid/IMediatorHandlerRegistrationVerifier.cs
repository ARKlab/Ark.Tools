// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.Solid;

/// <summary>Checks whether a mediator handler contract is registered.</summary>
public interface IMediatorHandlerRegistrationVerifier
{
    /// <summary>Checks whether the supplied handler contract is registered.</summary>
    /// <param name="handlerType">The closed handler contract to check.</param>
    /// <returns><see langword="true"/> when the handler contract is registered.</returns>
    bool IsRegistered(Type handlerType);
}

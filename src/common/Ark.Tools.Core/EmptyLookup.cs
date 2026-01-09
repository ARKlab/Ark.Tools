// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.Core;

public static class EmptyLookup<TKey, TElement>
{
    private static readonly ILookup<TKey, TElement> _instance
        = Enumerable.Empty<(TKey KeyType, TElement ValueType)>().ToLookup(x => x.KeyType, x => x.ValueType);

    public static ILookup<TKey, TElement> Instance
    {
        get { return _instance; }
    }
}
// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.Core
{
    public static class CollectionExtensions
    {
        public static List<T> ReplaceListElement<T>(this List<T> collection, T oldValue, T newValue)
        {
            var updatedCollection = collection.ToList();

            var index = collection.IndexOf(oldValue);

            updatedCollection[index] = newValue;

            return updatedCollection;
        }
    }
}
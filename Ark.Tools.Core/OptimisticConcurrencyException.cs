// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.Core
{
    public class OptimisticConcurrencyException : ApplicationException
    {
        public OptimisticConcurrencyException(string message)
            : base(message)
        {
        }

        public OptimisticConcurrencyException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }


        public OptimisticConcurrencyException(Exception inner, string message)
            : base(message, inner)
        {
        }

        public OptimisticConcurrencyException(Exception inner, string format, params object[] args)
            : base(string.Format(format, args), inner)
        {
        }

    }
}

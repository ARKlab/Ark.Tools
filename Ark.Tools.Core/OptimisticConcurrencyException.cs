// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Globalization;

namespace Ark.Tools.Core
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013:Types should not extend System.ApplicationException", Justification = "Historical mistake - public interface - Next Major")]
    public class OptimisticConcurrencyException : ApplicationException
    {
        public OptimisticConcurrencyException(string message)
            : base(message)
        {
        }

        public OptimisticConcurrencyException(string format, params object[] args)
            : base(string.Format(CultureInfo.InvariantCulture, format, args))
        {
        }


        public OptimisticConcurrencyException(Exception inner, string message)
            : base(message, inner)
        {
        }

        public OptimisticConcurrencyException(Exception inner, string format, params object[] args)
            : base(string.Format(CultureInfo.InvariantCulture, format, args), inner)
        {
        }

    }
}

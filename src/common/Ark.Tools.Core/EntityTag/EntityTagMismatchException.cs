// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Globalization;

namespace Ark.Tools.Core.EntityTag
{
    public class EntityTagMismatchException : Exception
    {
        public EntityTagMismatchException(string message)
            : base(message)
        {
        }

        public EntityTagMismatchException(string format, params object[] args)
            : base(string.Format(CultureInfo.InvariantCulture, format, args))
        {
        }


        public EntityTagMismatchException(Exception inner, string message)
            : base(message, inner)
        {
        }

        public EntityTagMismatchException(Exception inner, string format, params object[] args)
            : base(string.Format(CultureInfo.InvariantCulture, format, args), inner)
        {
        }

        public EntityTagMismatchException()
        {
        }

        public EntityTagMismatchException(string message, Exception innerException) : this(innerException, message)
        {
        }
    }
}

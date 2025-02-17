﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Globalization;

namespace Ark.Tools.Core
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013:Types should not extend System.ApplicationException", Justification = "Historical mistake - public interface - Next Major")]
    public class EntityNotFoundException : ApplicationException
    {
        public EntityNotFoundException(string message)
            : base(message)
        {
        }

        public EntityNotFoundException(string format, params object[] args)
            : base(string.Format(CultureInfo.InvariantCulture, format, args))
        {
        }


        public EntityNotFoundException(Exception inner, string message)
            : base(message, inner)
        {
        }

        public EntityNotFoundException(Exception inner, string format, params object[] args)
            : base(string.Format(CultureInfo.InvariantCulture, format, args), inner)
        {
        }

    }
}

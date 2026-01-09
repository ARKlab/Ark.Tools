// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Globalization;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013:Types should not extend System.ApplicationException", Justification = "Historical mistake - public interface - Next Major")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058:Types should not extend certain base types", Justification = "Historical mistake - public interface - Next Major")]
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

        public EntityNotFoundException()
        {
        }

        public EntityNotFoundException(string message, Exception innerException) : this(innerException, message)
        {
        }
=======
namespace Ark.Tools.Core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013:Types should not extend System.ApplicationException", Justification = "Historical mistake - public interface - Next Major")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058:Types should not extend certain base types", Justification = "Historical mistake - public interface - Next Major")]
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

    public EntityNotFoundException()
    {
    }

    public EntityNotFoundException(string message, Exception innerException) : this(innerException, message)
    {
>>>>>>> After


namespace Ark.Tools.Core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013:Types should not extend System.ApplicationException", Justification = "Historical mistake - public interface - Next Major")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058:Types should not extend certain base types", Justification = "Historical mistake - public interface - Next Major")]
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

    public EntityNotFoundException()
    {
    }

    public EntityNotFoundException(string message, Exception innerException) : this(innerException, message)
    {
    }
}
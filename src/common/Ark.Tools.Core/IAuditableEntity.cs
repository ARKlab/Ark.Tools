// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
    public interface IAuditableEntity
    {
        Guid AuditId { get; set; }
    }


=======
namespace Ark.Tools.Core;

public interface IAuditableEntity
{
    Guid AuditId { get; set; }
>>>>>>> After
    namespace Ark.Tools.Core;

    public interface IAuditableEntity
    {
        Guid AuditId { get; set; }
    }
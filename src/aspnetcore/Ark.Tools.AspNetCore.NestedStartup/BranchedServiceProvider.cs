// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.AspNetCore.NestedStartup;

internal sealed class BranchedServiceProvider : IServiceProvider
{
    private readonly IServiceProvider _parentService;
    private readonly IServiceProvider _service;

    public BranchedServiceProvider(IServiceProvider parentService, IServiceProvider service)
    {
        _parentService = parentService;
        _service = service;
    }

    public object? GetService(Type serviceType)
    {
        return _service.GetService(serviceType) ?? _parentService.GetService(serviceType);
    }
}
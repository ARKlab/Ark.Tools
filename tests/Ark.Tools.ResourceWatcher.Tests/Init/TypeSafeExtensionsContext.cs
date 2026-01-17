// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher.Testing;

using Reqnroll;

namespace Ark.Tools.ResourceWatcher.Tests.Init;

/// <summary>
/// Context for managing state in type-safe extensions tests.
/// Follows Driver pattern - stores test state for sharing across step definitions.
/// </summary>
[Binding]
public sealed class TypeSafeExtensionsContext
{
    private readonly List<ResourceState<VoidExtensions>> _voidStatesToSave = [];
    private readonly List<ResourceState<TestExtensions>> _typedStatesToSave = [];
    
    private IEnumerable<ResourceState<VoidExtensions>>? _loadedVoidStates;
    private IEnumerable<ResourceState<TestExtensions>>? _loadedTypedStates;
    
    // Public properties for injection by other step classes
    public ResourceState<VoidExtensions>? CurrentVoid { get; set; }
    public ResourceState<TestExtensions>? CurrentTyped { get; set; }
    
    // Lists for batch operations
    public List<ResourceState<VoidExtensions>> VoidStatesToSave => _voidStatesToSave;
    public List<ResourceState<TestExtensions>> TypedStatesToSave => _typedStatesToSave;
    
    // Loaded states from database
    public IEnumerable<ResourceState<VoidExtensions>>? LoadedVoidStates
    {
        get => _loadedVoidStates;
        set => _loadedVoidStates = value;
    }
    
    public IEnumerable<ResourceState<TestExtensions>>? LoadedTypedStates
    {
        get => _loadedTypedStates;
        set => _loadedTypedStates = value;
    }
    
    // State for verification scenarios
    public ResourceState<TestExtensions>? LoadedTypedResourceForVerification { get; set; }
    
    // Clear methods for state management
    public void ClearVoidStates()
    {
        _voidStatesToSave.Clear();
        LoadedVoidStates = null;
        CurrentVoid = null;
    }
    
    public void ClearTypedStates()
    {
        _typedStatesToSave.Clear();
        LoadedTypedStates = null;
        CurrentTyped = null;
        LoadedTypedResourceForVerification = null;
    }
    
    public void ClearAll()
    {
        ClearVoidStates();
        ClearTypedStates();
    }
}

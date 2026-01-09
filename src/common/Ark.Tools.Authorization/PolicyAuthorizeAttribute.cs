using System;

namespace Ark.Tools.Authorization;

/// <summary>
/// Specifies that the class or method that this attribute is applied to requires the specified authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PolicyAuthorizeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyAuthorizeAttribute"/> class. 
    /// </summary>
    public PolicyAuthorizeAttribute()
    {
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyAuthorizeAttribute"/> class. 
    /// </summary>
    /// <param name="policyName">The required policy.</param>
    public PolicyAuthorizeAttribute(string policyName)
    {
        this.PolicyName = policyName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyAuthorizeAttribute"/> class. 
    /// </summary>
    /// <param name="policyType">The required policy. Must implement <see cref="IAuthorizationPolicy"/></param>
    public PolicyAuthorizeAttribute(Type policyType)
    {
        if (!typeof(IAuthorizationPolicy).IsAssignableFrom(policyType) || policyType.GetConstructor(Type.EmptyTypes) == null)
            throw new ArgumentException("should implement IAuthorizationPolicy", nameof(policyType));

        this.Policy = (IAuthorizationPolicy?)Activator.CreateInstance(policyType);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyAuthorizeAttribute"/> class. 
    /// </summary>
    /// <param name="policyType">The required policy. Must implement <see cref="IAuthorizationPolicy"/></param>
    /// <param name="args">Args for the policy</param>
    public PolicyAuthorizeAttribute(Type policyType, params object[] args)
    {
        if (!typeof(IAuthorizationPolicy).IsAssignableFrom(policyType))
            throw new ArgumentException("should implement IAuthorizationPolicy", nameof(policyType));

        this.Policy = (IAuthorizationPolicy?)Activator.CreateInstance(policyType, args);
    }

    /// <summary>
    /// The required policy.
    /// </summary>
    public string? PolicyName { get; }

    /// <summary>
    /// The required policy.
    /// </summary>
    public IAuthorizationPolicy? Policy { get; }
}


namespace Ark.Tools.Authorization.Requirement;

/// <summary>
/// Implements an <see cref="IAuthorizationHandler"/> and <see cref="IAuthorizationRequirement"/>
/// which requires at least one instance of the specified claim type, and, if allowed values are specified, 
/// the claim value must be any of the allowed values.
/// </summary>
public sealed class ClaimsAuthorizationRequirement : AuthorizationHandler<ClaimsAuthorizationRequirement>, IAuthorizationRequirement
{
    // Null when any claim value is allowed.
    private readonly HashSet<string>? _allowedValues;

    /// <summary>
    /// Creates a new instance of <see cref="ClaimsAuthorizationRequirement"/>.
    /// </summary>
    /// <param name="claimType">The claim type that must be present.</param>
    /// <param name="allowedValues">The optional list of claim values, which, if present, 
    /// the claim must match.</param>
    public ClaimsAuthorizationRequirement(string claimType, IEnumerable<string>? allowedValues)
    {
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        AllowedValues = allowedValues;
        if (allowedValues is not null)
        {
            var values = new HashSet<string>(allowedValues, StringComparer.Ordinal);
            if (values.Count > 0)
                _allowedValues = values;
        }
    }

    /// <summary>
    /// Gets the claim type that must be present.
    /// </summary>
    public string ClaimType { get; }

    /// <summary>
    /// Gets the optional list of claim values, which, if present, 
    /// the claim must match.
    /// </summary>
    public IEnumerable<string>? AllowedValues { get; }

    /// <summary>
    /// Makes a decision if authorization is allowed based on the claims requirements specified.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The requirement to evaluate.</param>
    /// <param name="ctk">CancellationToken</param>
    protected override Task HandleRequirementAsync(AuthorizationContext context, ClaimsAuthorizationRequirement requirement, CancellationToken ctk = default)
    {
        if (context.User != null)
        {
            foreach (var claim in context.User.Claims)
            {
                if (string.Equals(claim.Type, requirement.ClaimType, StringComparison.OrdinalIgnoreCase)
                    && (requirement._allowedValues is null || requirement._allowedValues.Contains(claim.Value)))
                {
                    context.Succeed(requirement);
                    break;
                }
            }
        }
        return Task.CompletedTask;
    }
}
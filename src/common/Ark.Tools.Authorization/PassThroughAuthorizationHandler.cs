
namespace Ark.Tools.Authorization;

/// <summary>
/// Infrastructre class which allows an <see cref="IAuthorizationRequirement"/> to
/// be its own <see cref="IAuthorizationHandler"/>.
/// </summary>
public class PassThroughAuthorizationHandler : IAuthorizationHandler
{
    /// <summary>
    /// Makes a decision if authorization is allowed.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="ctk">Cancellation Token</param>
    public async Task HandleAsync(AuthorizationContext context, CancellationToken ctk = default)
    {
        foreach (var handler in context.Policy.Requirements.OfType<IAuthorizationHandler>())
        {
            await handler.HandleAsync(context, ctk).ConfigureAwait(false);
        }
    }
}
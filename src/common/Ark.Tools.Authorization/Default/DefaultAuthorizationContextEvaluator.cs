
namespace Ark.Tools.Authorization;

public sealed class DefaultAuthorizationContextEvaluator : IAuthorizationContextEvaluator
{
    public (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext)
    {
        return (authContext.HasSucceeded, new List<string>(authContext.Messages.Values));
    }
}

namespace Ark.Tools.Authorization;

public class DefaultAuthorizationContextEvaluator : IAuthorizationContextEvaluator
{
    public (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext)
    {
        return (authContext.HasSucceeded, authContext.Messages.Select(s => s.Value).ToList());
    }
}
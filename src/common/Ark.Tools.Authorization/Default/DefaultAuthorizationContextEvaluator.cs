using System.Collections.Generic;
using System.Linq;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Authorization(net10.0)', Before:
namespace Ark.Tools.Authorization
{
    public class DefaultAuthorizationContextEvaluator : IAuthorizationContextEvaluator
    {
        public (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext)
        {
            return (authContext.HasSucceeded, authContext.Messages.Select(s => s.Value).ToList());
        }
=======
namespace Ark.Tools.Authorization;

public class DefaultAuthorizationContextEvaluator : IAuthorizationContextEvaluator
{
    public (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext)
    {
        return (authContext.HasSucceeded, authContext.Messages.Select(s => s.Value).ToList());
>>>>>>> After


namespace Ark.Tools.Authorization;

public class DefaultAuthorizationContextEvaluator : IAuthorizationContextEvaluator
{
    public (bool authorized, IList<string> messages) Evaluate(AuthorizationContext authContext)
    {
        return (authContext.HasSucceeded, authContext.Messages.Select(s => s.Value).ToList());
    }
}
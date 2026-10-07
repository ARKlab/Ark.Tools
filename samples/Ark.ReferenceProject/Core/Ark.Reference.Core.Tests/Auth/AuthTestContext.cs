using Ark.Reference.Core.InProcessHost.Auth;
using Ark.Tools.Compliance;

using Flurl.Http;

using Reqnroll;

namespace Ark.Reference.Core.Tests.Auth;

[Binding]
public class AuthTestContext
{
    [InfrastructureSecret]
    public const string AUTH0_APIKEY = "banana";
    [InfrastructureSecret]
    public string Token => _auth.Token;

    [InfrastructureSecret]
    public string? ApiKey => _auth.ApiKey;

    private readonly ApiAuthContext _auth = new();

    [Given("User '(.*)'")]
    public void SetUser(string user)
    {
        _auth.SetUser(user);
    }

    [Given("User email '(.*)'")]
    public void SetUserEmail(
        [PersonalData]
        string userEmail)
    {
        _auth.SetUserEmail(userEmail);
    }

    [Given("Admin User")]
    public void SetUserAdmin()
    {
        _auth.SetUserAdmin();
    }


    [Given("Subject '(.*)'")]
    public void SetSubject(string subject)
    {
        _auth.SetSubject(subject);
    }

    public IFlurlRequest SetAuth(IFlurlRequest request)
    {
        return _auth.SetAuth(request);
    }

    [BeforeScenario]
    public void SetAuthUser(ScenarioContext sctx, FeatureContext fctx)
    {
        SetUser(ComplianceFakes.Email());
    }

    [Given(@"User scopes as")]
    public void GivenUserScopesAs(Table table)
    {
        _auth.SetScopes(table.Rows.SelectMany(static x => x.Values));
    }
    [Given(@"User has no Permissions")]
    public void GivenUserNoScopes()
    {
        _auth.SetScopes([]);
    }

    [Given(@"User has scope '(.*)'")]
    public void GivenUserHasScope(string scope)
    {
        _auth.AddScope(scope);
    }

    [Given(@"New User with scope '(.*)'")]
    public void GivenNewUserWithScope(string scope)
    {
        GivenUserNoScopes();
        _auth.AddScope(scope);
        _ = _auth.Token;
    }

}
using Ark.Reference.Common.Auth;
using Ark.Reference.Core.Common.Auth;

using Flurl.Http;

using Microsoft.IdentityModel.Tokens;

using Reqnroll;

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ark.Reference.Core.Tests.Auth;

[Binding]
public class AuthTestContext
{
    public const string AUTH0_APIKEY = "banana";
    public string Token => _getToken();

    public string? ApiKey { get; private set; }

    private readonly JwtTokenBuilder _builder = new JwtTokenBuilder()
                            .AddSecurityKey(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey)))
                            .AddSubject("TestSubject")
                            .AddAudience(AuthConstants.IntegrationTestsAudience)
                            .AddIssuer($"https://{AuthConstants.IntegrationTestsDomain}/")
                            .AddExpiry(60)
                            ;

    private readonly string _scopeClaim = AuthConstants.ScopePrefix;
    private List<string> _scopes = new();

    public AuthTestContext()
    {
        SetUserAdmin();
    }

    [Given("User '(.*)'")]
    public void SetUser(string user)
    {
        _builder.AddSubject(user);
        _builder.RemoveClaim("user_id");
        _builder.AddClaim("user_id", user);

        _builder.RemoveClaim("name");
        _builder.AddClaim("name", user);
    }

    [Given("User email '(.*)'")]
    public void SetUserEmail(string userEmail)
    {
        _builder.RemoveClaim("emails");
        _builder.AddClaim("emails", userEmail);
    }

    [Given("Admin User")]
    public void SetUserAdmin()
    {
        SetUser("Admin");
        //_scopes = AuthScopes.All.ToList();
        _scopes.Add(PermissionsConstants.AdminGrant);
    }


    [Given("Subject '(.*)'")]
    public void SetSubject(string subject)
    {
        ApiKey = null;

        _builder.AddSubject(subject);
    }

    public IFlurlRequest SetAuth(IFlurlRequest request)
    {
        if (ApiKey != null)
            request.WithHeader("x-api-key", ApiKey);
        else
            request.WithOAuthBearerToken(Token);
        return request;
    }

    [BeforeScenario]
    public void SetAuthUser(ScenarioContext sctx, FeatureContext fctx)
    {
        SetUser("testUser1@ark-energy.eu");
    }

    [Given(@"User scopes as")]
    public void GivenUserScopesAs(Table table)
    {
        _scopes = table.Rows.SelectMany(x => x.Values).ToList();
    }
    [Given(@"User has no Permissions")]
    public void GivenUserNoScopes()
    {
        _scopes = new List<string>();
    }

    [Given(@"User has scope '(.*)'")]
    public void GivenUserHasScope(string scope)
    {
        _scopes.Add(scope);
    }

    [Given(@"New User with scope '(.*)'")]
    public void GivenNewUserWithScope(string scope)
    {
        GivenUserNoScopes();
        _scopes.Add(scope);
        _getToken();
    }

    private string _getToken()
    {
        _builder.RemoveClaim(_scopeClaim);

        foreach (var s in _scopes)
            _builder.AddClaim(_scopeClaim, s);

        return _builder.Build().Value;

    }

}
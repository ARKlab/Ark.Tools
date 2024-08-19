using Flurl.Http;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Text;
using TechTalk.SpecFlow;


namespace RavenDbSample.Tests.AuthTest
{
	[Binding]
	public class AuthTestContext
	{
		public enum Role
		{
			Admin,
			User
		}

		public string Token => _builder.Build().Value;

		private JwtTokenBuilder _builder = new JwtTokenBuilder()
								.AddSecurityKey(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(ApplicationConstants.ClientSecretSpecFlow)))
								.AddSubject("TestSubject")
								.AddIssuer($"https://{Program.Configuration["Auth0:Domain"]}/")
								.AddAudience(Program.Configuration["Auth0:Audience"])
								.AddExpiry(60)
								;

		[Given("Role '(.*)'")]
		public void SetRole(Role role)
		{
			_builder.RemoveClaims(ApplicationConstants.ClaimScope);

			var scopesSet = new HashSet<string>();

			if (role == Role.Admin)
			{
				//READ and WRITE
			}

			if (role == Role.User)
			{
				//Only READ
			}

			_builder.AddClaim(ApplicationConstants.ClaimScope, string.Join(' ', scopesSet));
		}

		[Given("Subject '(.*)'")]
		public void SetSubject(string subject)
		{
			_builder.AddSubject(subject);
		}


		public IFlurlRequest SetAuth(IFlurlRequest request)
		{
			if (Token != null)
				request.WithOAuthBearerToken(Token);

			return request;
		}
	}
}

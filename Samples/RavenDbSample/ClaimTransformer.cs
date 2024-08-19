using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public class ClaimsTransformer : IClaimsTransformation
	{
		public ClaimsTransformer(IHttpContextAccessor httpAccessor)
		{
		}

		public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
		{
			(principal.Identity as ClaimsIdentity).AddClaim(new Claim("Anonymous", "role"));

			return Task.FromResult(principal);
		}
	}
}
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Threading.Tasks;

public class TransformEmailClaim : IClaimsTransformation
{
    public TransformEmailClaim()
    {
        // Constructor logic here if needed
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(c => c.Type == ClaimTypes.Email))
            return principal;

        // If it does not exist, look for the custom email claim
        var emailClaim = principal.FindFirst(c => c.Type == "emails");
        if (emailClaim == null)
            return principal;

        // Clone the current principal to avoid modifying the existing principal directly
        var clone = principal.Clone();

        // Create a new claim with the standard email claim type
        var newEmailClaim = new Claim(ClaimTypes.Email, emailClaim.Value);
        // Add the new claim to the cloned identity
        (clone.Identity as ClaimsIdentity)?.AddClaim(newEmailClaim);
        return clone;
        
    }
}

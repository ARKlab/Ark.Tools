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
        // Clone the current principal to avoid modifying the existing principal directly
        var clone = principal.Clone();
        var newIdentity = (ClaimsIdentity)clone.Identity;

        // Check if the email claim already exists in the standard format
        var existingClaim = newIdentity.FindFirst(ClaimTypes.Email);
        if (existingClaim == null)
        {
            // If it does not exist, look for the custom email claim
            var emailClaim = newIdentity.FindFirst(c => c.Type == "emails");
            if (emailClaim != null)
            {
                // Create a new claim with the standard email claim type
                var newEmailClaim = new Claim(ClaimTypes.Email, emailClaim.Value);

                // Add the new claim to the cloned identity
                newIdentity.AddClaim(newEmailClaim);
            }
        }

        // Return the modified principal
        return clone;
    }
}

using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Ark.Reference.Core.Tests.Auth
{
    public static class JwtSecurityKey
    {
        public static SymmetricSecurityKey Create(string secret)
        {
            return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));
        }
    }
}
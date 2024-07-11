using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Services.Auth
{
	public interface IClientAuthenticationService
	{
		Task<string> GetTokenAsync(string scope, CancellationToken ctk = default);
	}
}

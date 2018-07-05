using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface IRequestProcessor
    {
        TResponse Execute<TResponse>(IRequest<TResponse> request);

        Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default(CancellationToken));
    }
}

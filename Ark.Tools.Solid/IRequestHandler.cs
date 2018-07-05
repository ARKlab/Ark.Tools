using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface IRequest<TResponse>
    {
    }

    public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        TResponse Execute(TRequest Request);

        Task<TResponse> ExecuteAsync(TRequest Request, CancellationToken ctk = default(CancellationToken));
    }
}

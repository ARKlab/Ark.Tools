using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface IQueryProcessor
    {
        TResult Execute<TResult>(IQuery<TResult> query);

        Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default(CancellationToken));
    }
}

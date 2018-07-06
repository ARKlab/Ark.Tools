using System.Collections.Generic;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{
    public interface IStateProvider
    {
        Task<IEnumerable<ResourceState>> LoadStateAsync(string tenant, string[] resourceIds = null, CancellationToken ctk = default(CancellationToken));
        Task SaveStateAsync(IEnumerable<ResourceState> states, CancellationToken ctk = default(CancellationToken));
    } 

}
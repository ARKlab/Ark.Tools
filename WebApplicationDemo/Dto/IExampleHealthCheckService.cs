using System.Threading;
using System.Threading.Tasks;

namespace WebApplicationDemo.Dto
{
    public interface IExampleHealthCheckService
    {
        public Task CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}

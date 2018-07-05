using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface ICommandProcessor
    {
        void Execute(object command);

        Task ExecuteAsync(object command, CancellationToken ctk = default(CancellationToken));
    }
}

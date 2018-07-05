using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid
{
    public interface ICommandHandler<TCommand>
    {
        void Execute(TCommand command);

        Task ExecuteAsync(TCommand command, CancellationToken ctk = default(CancellationToken));
    }

}

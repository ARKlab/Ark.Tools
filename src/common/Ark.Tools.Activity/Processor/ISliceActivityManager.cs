using System.Threading.Tasks;

namespace Ark.Tools.Activity.Processor
{
    public interface ISliceActivityManager
    {
        Task Start();
    }

    public interface ISliceActivityManager<T> : ISliceActivityManager where T : class, ISliceActivity
    {
    }


}
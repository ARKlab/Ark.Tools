using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
namespace Ark.Tools.Activity.Processor
{
    public interface ISliceActivityManager
    {
        Task Start();
    }

    public interface ISliceActivityManager<T> : ISliceActivityManager where T : class, ISliceActivity
    {
    }


=======
namespace Ark.Tools.Activity.Processor;

public interface ISliceActivityManager
{
    Task Start();
}

public interface ISliceActivityManager<T> : ISliceActivityManager where T : class, ISliceActivity
{
>>>>>>> After
    namespace Ark.Tools.Activity.Processor;

    public interface ISliceActivityManager
    {
        Task Start();
    }

    public interface ISliceActivityManager<T> : ISliceActivityManager where T : class, ISliceActivity
    {
    }
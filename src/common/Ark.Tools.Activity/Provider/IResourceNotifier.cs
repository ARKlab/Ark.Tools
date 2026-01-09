using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
namespace Ark.Tools.Activity.Provider
{
    public interface IResourceNotifier
    {
        string Provider { get; }
        Task Notify(string resourceId, Slice slice);
    }
=======
namespace Ark.Tools.Activity.Provider;

public interface IResourceNotifier
{
    string Provider { get; }
    Task Notify(string resourceId, Slice slice);
>>>>>>> After


namespace Ark.Tools.Activity.Provider;

public interface IResourceNotifier
{
    string Provider { get; }
    Task Notify(string resourceId, Slice slice);
}
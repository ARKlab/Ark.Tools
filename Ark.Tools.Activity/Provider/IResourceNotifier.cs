using System.Threading.Tasks;

namespace Ark.Tools.Activity.Provider
{
    public interface IResourceNotifier
    {
        string Provider { get; }
        Task Notify(string resourceId, Slice slice);
    }
}
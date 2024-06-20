using Rebus.Handlers;

namespace Core.Service.Application.Handlers
{
    // main Core queue
    public interface IHandleMessagesCore<in T> : IHandleMessages<T> where T : class
    {
    }

    // Artesian Core subqueue
    public interface IHandleMessagesCoreArtesian<in T> : IHandleMessages<T> where T : class
    {
    }
}

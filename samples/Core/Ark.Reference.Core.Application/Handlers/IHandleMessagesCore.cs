using Rebus.Handlers;

namespace Ark.Reference.Core.Application.Handlers
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

namespace Ark.Tools.Outbox;


public interface IOutboxContextFactory
{
    IOutboxContext Create();
}
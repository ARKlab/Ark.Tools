namespace Ark.Reference.Core.Application.Handlers.Messages;

public static class MessageCounter
{
    private static int _messageCount;

    public static int Increment()
    {
        return ++_messageCount;
    }

    public static int GetCount()
    {
        return _messageCount;
    }

    public static void ResetCount()
    {
        _messageCount = 0;
    }
}
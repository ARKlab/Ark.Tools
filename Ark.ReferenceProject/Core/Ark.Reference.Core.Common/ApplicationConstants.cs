namespace Ark.Reference.Core.Common
{
    public class ApplicationConstants
    {
        public static readonly string[] Versions = {
             "1.0"
        };
    }

    public static class MessageCounter
    {
        private static int _messageCount = 0;

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
}

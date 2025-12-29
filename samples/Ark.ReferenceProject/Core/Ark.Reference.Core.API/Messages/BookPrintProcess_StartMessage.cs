namespace Ark.Reference.Core.API.Messages
{
    /// <summary>
    /// Message to start processing a book print
    /// </summary>
    public static class BookPrintProcess_StartMessage
    {
        /// <summary>
        /// Version 1 of the message
        /// </summary>
        public record V1
        {
            /// <summary>
            /// Gets or initializes the book print process ID
            /// </summary>
            public int BookPrintProcessId { get; init; }

            /// <summary>
            /// Gets or initializes whether the process should fail (for testing)
            /// </summary>
            public bool ShouldFail { get; init; }
        }
    }
}

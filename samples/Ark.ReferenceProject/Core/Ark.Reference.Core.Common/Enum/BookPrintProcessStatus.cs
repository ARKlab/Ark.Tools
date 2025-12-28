namespace Ark.Reference.Core.Common.Enum
{
    /// <summary>
    /// Status of a book print process
    /// </summary>
    public enum BookPrintProcessStatus
    {
        /// <summary>
        /// Print process is pending and waiting to start
        /// </summary>
        Pending,

        /// <summary>
        /// Print process is currently running
        /// </summary>
        Running,

        /// <summary>
        /// Print process completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Print process failed with an error
        /// </summary>
        Error
    }
}

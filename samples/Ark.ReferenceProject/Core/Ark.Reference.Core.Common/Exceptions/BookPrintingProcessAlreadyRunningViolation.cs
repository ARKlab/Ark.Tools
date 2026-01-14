namespace Ark.Reference.Core.Common.Exceptions;

/// <summary>
/// Business rule violation indicating that a book print process is already running for a given book.
/// The class name itself serves as the error code for this specific violation.
/// </summary>
public class BookPrintingProcessAlreadyRunningViolation : Tools.Core.BusinessRuleViolation.BusinessRuleViolation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookPrintingProcessAlreadyRunningViolation"/> class
    /// </summary>
    /// <param name="bookId">The ID of the book that already has a running print process</param>
    public BookPrintingProcessAlreadyRunningViolation(int bookId)
        : base($"A print process is already running or pending for this book")
    {
        BookId = bookId;
        Detail = $"Cannot start a new print process for book ID {bookId} because another print process is already running or pending for this book.";
    }

    /// <summary>
    /// Gets the ID of the book that already has a running print process
    /// </summary>
    public int BookId { get; }
}
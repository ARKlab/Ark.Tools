namespace Ark.Reference.Core.Common.Exceptions
{
    /// <summary>
    /// Represents a business rule violation (AspNetCore-agnostic)
    /// </summary>
    public record BusinessRuleViolation
    {
        /// <summary>
        /// Gets or initializes the type of violation
        /// </summary>
        public string? Type { get; init; }

        /// <summary>
        /// Gets or initializes the title of the violation
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Gets or initializes the detailed description of the violation
        /// </summary>
        public string? Detail { get; init; }

        /// <summary>
        /// Gets or initializes the business rule code
        /// </summary>
        public string? RuleCode { get; init; }
    }
}

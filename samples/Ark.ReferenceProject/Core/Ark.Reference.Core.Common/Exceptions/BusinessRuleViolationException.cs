using System;
using System.Diagnostics.CodeAnalysis;

namespace Ark.Reference.Core.Common.Exceptions
{
    /// <summary>
    /// Exception thrown when a business rule is violated.
    /// This exception is designed to be created only with a specific rule code and message.
    /// </summary>
    [SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "BusinessRuleViolationException is meant to be created only with a rule code and message")]
    public class BusinessRuleViolationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class
        /// </summary>
        /// <param name="ruleCode">The business rule code that was violated</param>
        /// <param name="message">The error message</param>
        public BusinessRuleViolationException(string ruleCode, string message)
            : base(message)
        {
            RuleCode = ruleCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class
        /// </summary>
        /// <param name="ruleCode">The business rule code that was violated</param>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public BusinessRuleViolationException(string ruleCode, string message, Exception innerException)
            : base(message, innerException)
        {
            RuleCode = ruleCode;
        }

        /// <summary>
        /// Gets the business rule code that was violated
        /// </summary>
        public string RuleCode { get; }

        /// <summary>
        /// Converts the exception to a BusinessRuleViolation record
        /// </summary>
        public BusinessRuleViolation ToViolation() => new()
        {
            Type = "https://example.com/probs/business-rule-violation",
            Title = "Business Rule Violation",
            Detail = Message,
            RuleCode = RuleCode
        };
    }
}

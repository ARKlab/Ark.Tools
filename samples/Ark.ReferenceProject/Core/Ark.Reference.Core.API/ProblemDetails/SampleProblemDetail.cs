using Ark.Tools.Core.BusinessRuleViolation;

namespace Ark.Reference.Core.API.ProblemDetails
{
    public class SampleProblemDetail : BusinessRuleViolation
    {

        public SampleProblemDetail(int amount)
           : base("SAMPLE_PROBLEM")
        {
            Status = 400;
            Amount = amount;
            Detail = $"The amount is not nice: {amount}";
        }

        public int Amount { get; }
    }
}
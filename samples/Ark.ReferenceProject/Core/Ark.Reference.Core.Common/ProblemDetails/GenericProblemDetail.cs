using Ark.Tools.Core.BusinessRuleViolation;



namespace Ark.Reference.Core.Common.ProblemDetails
{
    public class GenericProblemDetail : BusinessRuleViolation
    {
        public GenericProblemDetail(string name)
            : base("GENERIC_PROBLEM_DETAILS")
        {
            Status = 400;
            Name = name;

            Detail = $"Generic Problem Details: '{name}' does not exists";
        }

        public string Name { get; set; }
    }
}
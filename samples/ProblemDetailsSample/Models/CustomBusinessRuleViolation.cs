using Ark.Tools.Core.BusinessRuleViolation;


namespace ProblemDetailsSample.Models;

public class CustomBusinessRuleViolation : BusinessRuleViolation
{
    public CustomBusinessRuleViolation()
        : base("You are my son!")
    {
        Accounts = new List<string>();
    }

    public decimal Balance { get; set; }

    public ICollection<string> Accounts { get; }
}
using Ark.Tools.AspNetCore.ProblemDetails;


namespace ProblemDetailsSample.Models;

public class OutOfCreditProblemDetails : ArkProblemDetails
{
    public OutOfCreditProblemDetails()
        : base("You do not have enough credit.")
    {
        Accounts = new List<string>();
    }

    public decimal Balance { get; set; }

    public ICollection<string> Accounts { get; }
}
using Ark.Tools.AspNetCore.ProblemDetails;

using System.Collections.Generic;

namespace WebApplicationDemo.Dto;

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
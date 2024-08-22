using System.Collections.Generic;
using Ark.Tools.AspNetCore.ProblemDetails;

namespace WebApplicationDemo.Dto
{
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
}
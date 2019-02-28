using System.Collections.Generic;
using Ark.Tools.AspNetCore.ProbDetails;
using Microsoft.AspNetCore.Mvc;

namespace ProblemDetailsSample.Models
{
    public class OutOfCreditProblemDetails : ArkProblemDetails
    {
        public OutOfCreditProblemDetails(string title) : base(title)
        {
            Accounts = new List<string>();
        }

        public decimal Balance { get; set; }

        public ICollection<string> Accounts { get; }
    }
}
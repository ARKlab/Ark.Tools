using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace ProblemDetailsSample.Models
{

    //To change and use ArkProblemDetails
    public class OutOfCreditProblemDetails : ProblemDetails
    {
        public OutOfCreditProblemDetails()
        {
            Accounts = new List<string>();
        }

        public decimal Balance { get; set; }

        public ICollection<string> Accounts { get; }
    }
}
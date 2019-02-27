using System.Collections.Generic;
using Ark.Tools.AspNetCore.ProbDetails;
using Microsoft.AspNetCore.Mvc;

namespace ProblemDetailsSample.Models
{

    //To change and use ArkProblemDetails
    public class OutOfCreditProblemDetails : ArkProblemDetails
    {
        public OutOfCreditProblemDetails()
        {
            Accounts = new List<string>();
        }

        public decimal Balance { get; set; }

        public ICollection<string> Accounts { get; }
    }

    //public class TestProblemDetails : ArkProblemDetails
    //{
    //    public TestProblemDetails()
    //    {
    //        Accounts = new List<string>();
    //    }

    //    public decimal Balance { get; set; }

    //    public ICollection<string> Accounts { get; }
    //}
}
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.ProbDetails
{
    public class ArkProblemDetails : ProblemDetails
    {
        public ArkProblemDetails(string type, string title)
        {
            Type = type;
            Title = title;
        }

    }
}

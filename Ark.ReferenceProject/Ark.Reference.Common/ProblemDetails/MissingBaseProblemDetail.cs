using Ark.Tools.Core.BusinessRuleViolation;

namespace Ark.Reference.Common.ProblemDetails
{
    public class MissingBaseProblemDetail : BusinessRuleViolation
    {

        public MissingBaseProblemDetail(string title, int sourceCurveId)
           : base(title)
        {
            Status = 400;
            SourceCurveId = sourceCurveId;
        }

        public int SourceCurveId { get; set; }
    }
}

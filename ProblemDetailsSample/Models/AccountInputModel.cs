using System.ComponentModel.DataAnnotations;

namespace ProblemDetailsSample.Models
{
    public class AccountInputModel
    {
        [Required] public int? AccountNumber { get; set; }

        [Required] public string Reference { get; set; }
    }
}
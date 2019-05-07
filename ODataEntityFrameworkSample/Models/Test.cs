using System.ComponentModel.DataAnnotations;

namespace ODataEntityFrameworkSample.Models
{
	public class Test
	{
		[Key]
		public string Id { get; set; }
		public int Value { get; set; }
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Models
{
	public class Test
	{
		[Key]
		public string Id { get; set; }
		public int Value { get; set; }
	}
}

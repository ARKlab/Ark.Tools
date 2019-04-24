using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Models
{
	public class Country /*: IAuditable*/
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public virtual HashSet<City> Cities { get; set; }
	}

	public class City
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public int CountryId { get; set; }
		public int? Population { get; set; }
	}
}

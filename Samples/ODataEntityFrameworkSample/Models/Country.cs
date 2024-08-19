using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ODataEntityFrameworkSample.Models
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

	//**********************************************************************//
	public class CountryDto /*: IAuditable*/
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public virtual HashSet<CityDto> Cities { get; set; }
	}

	public class CityDto
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public int CountryId { get; set; }
		public int? Population { get; set; }
	}
}

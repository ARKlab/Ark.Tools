using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Microsoft.AspNet.OData.Builder;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ODataEntityFrameworkSample.Models
{
	public class University/* : IAuditableEntityFramework*/
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }

		[Contained]
		public virtual HashSet<Person> People { get; set; }
	}

	[Owned]
	public class Person
	{
		public string Name { get; set; }
		public string SurName { get; set; }
		public Role Role { get; set; }

		public string Field { get; set; }
	}

	public enum Role
	{
		Professor,
		Associated,
	}

	//***********************************************************//

	//public class UniversityDto
	//{
	//	[Key]
	//	public int Id { get; set; }
	//	public string Name { get; set; }

	//	[Contained]
	//	public virtual HashSet<PersonDto> People { get; set; }
	//}

	//[Owned]
	//public class PersonDto
	//{
	//	public string Name { get; set; }
	//	public string SurName { get; set; }
	//	public Role Role { get; set; }

	//	public string Field { get; set; }
	//}
}

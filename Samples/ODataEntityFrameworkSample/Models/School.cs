using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Microsoft.AspNet.OData.Builder;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ODataEntityFrameworkSample.Models
{
	public class School/* : IAuditableEntityFramework*/
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }

		[Contained]
		public virtual HashSet<Student> Students { get; set; }

		[Contained]
		public virtual Registry Registry { get; set; }
	}

	[Owned]
	public class Student
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public string Power { get; set; }
	}

	[Owned]
	public class Registry
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public HashSet<Rule> Rules { get; set; }
	}

	[Owned]
	public class Rule
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public int? Value { get; set; }
	}
	//***********************************************************//
	public class SchoolDto
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }

		[Contained]
		public virtual HashSet<StudentDto> Students { get; set; }

		[Contained]
		public virtual RegistryDto Registry { get; set; }
	}

	[Owned]
	public class StudentDto
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public string Power { get; set; }
	}

	[Owned]
	public class RegistryDto
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public HashSet<RuleDto> Rules { get; set; }
	}

	[Owned]
	public class RuleDto
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public int? Value { get; set; }
	}

}

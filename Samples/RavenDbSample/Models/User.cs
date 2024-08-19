using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RavenDbSample.Models
{
	public class User
	{
		[Key]
		public string Id { get; set; }
		public HashSet<RoleEnum> Roles { get; set; }
	}

	public enum RoleEnum
	{
		Admin,
	}
}

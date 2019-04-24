using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ODataSample.Models
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

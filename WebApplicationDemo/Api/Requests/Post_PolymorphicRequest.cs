using Ark.Tools.Solid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Requests
{
	public class Post_PolymorphicRequest
	{
		public class V1 : IRequest<Polymorphic?>
		{
			public Polymorphic? Entity { get; set; }
		}
	}
}

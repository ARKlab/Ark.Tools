using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplicationDemo.Dto
{
	public static class Examples
	{
		public static Entity.V1.Output GeEntityPayload()
		{
			return new Entity.V1.Output()
			{
				EntityId = "Identifier",
				Date = new NodaTime.LocalDate(2019, 01, 01),
				Value = 1000,
				EntityResult = EntityResult.Success2,
				EntityTest =  EntityTest.Prova1			
			};

		}
	}
}

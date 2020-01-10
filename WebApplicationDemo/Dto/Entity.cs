using Ark.Tools.Core.EntityTag;
using NodaTime;
using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationDemo.Dto
{
    public static class Entity
    {
        public static class V1
        {
            public class Input : IEntityWithETag
            {
                public virtual string _ETag { get; set; }

				[Required]
                public string EntityId { get; set; }

				public EntityResult EntityResult { get; set; }

				public EntityTest EntityTest { get; set; }
			}

            public class Output : Input
            {
                public int Value { get; set; }
				public LocalDate Date { get; set; }

			}
        }

    }


	[Flags]
	public enum EntityResult
	{
		None = 0,
		Success1 = 1<<1,
		Success2 = 1<<2
	}

	public enum EntityTest
	{
		Prava0,
		Prova1
	}
}

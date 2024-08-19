using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;
using NodaTime;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationDemo.Dto
{
    public static class Entity
    {
        public static class V1
        {
			public record Input : IEntityWithETag
			{
				public virtual string? _ETag { get; set; }

				[Required]
				public string? EntityId { get; set; }

				public EntityResult EntityResult { get; set; }

				public EntityTest? EntityTest { get; set; }

				public ValueCollection<string>? Strings { get; set; }

				public IDictionary<LocalDate, double?> Ts { get; set; } = new Dictionary<LocalDate, double?>();

			}

            public record Output : Input
            {
                public Output() { }
                public Output(Input other)
                {
                    _ETag= other._ETag;
                    EntityId = other.EntityId;
                    EntityResult = other.EntityResult;
                    Strings = other.Strings;
                    Ts = other.Ts;
                }

                public int Value { get; set; }
				public LocalDate? Date { get; set; }
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

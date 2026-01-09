using Ark.Tools.Core;
using Ark.Tools.Core.EntityTag;

using MessagePack;

using NodaTime;

using System.ComponentModel.DataAnnotations;

namespace WebApplicationDemo.Dto;

public static class Entity
{
    public static class V1
    {
        [MessagePackObject]
        public record Input : IEntityWithETag
        {
            [MessagePack.Key(0)]
            [Required]
            public string? EntityId { get; set; }
            [MessagePack.Key(1)]
            public virtual string? _ETag { get; set; }

            [MessagePack.Key(2)]
            public EntityResult EntityResult { get; set; }

            [MessagePack.Key(3)]
            public EntityTest? EntityTest { get; set; }

            [MessagePack.Key(4)]
            public ValueCollection<string> Strings { get; set; } = new ValueCollection<string>(StringComparer.Ordinal);

            [MessagePack.Key(5)]
            public IDictionary<LocalDate, double?> Ts { get; set; } = new Dictionary<LocalDate, double?>();

        }

        [MessagePackObject]
        public record Output : Input
        {
            public Output() { }
            public Output(Input other)
            {
                _ETag = other._ETag;
                EntityId = other.EntityId;
                EntityResult = other.EntityResult;
                Strings = other.Strings;
                Ts = other.Ts;
            }


            [MessagePack.Key(6)]
            public int Value { get; set; }

            [MessagePack.Key(7)]
            public LocalDate? Date { get; set; }
        }
    }

}


[Flags]
public enum EntityResult
{
    None = 0,
    Success1 = 1 << 1,
    Success2 = 1 << 2
}

public enum EntityTest
{
    Prava0,
    Prova1
}
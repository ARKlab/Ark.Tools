using System;

namespace Ark.Tools.EventSourcing.Aggregates
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class AggregateNameAttribute : Attribute
    {
        public string Name { get; }

        public AggregateNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EventNameAttribute : Attribute
    {
        public string Name { get; }

        public EventNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EventVersionAttribute : Attribute
    {
        public int Version { get; }

        public EventVersionAttribute(int version)
        {
            Version = version;
        }
    }
}

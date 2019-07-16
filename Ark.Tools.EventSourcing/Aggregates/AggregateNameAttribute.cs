using System;

namespace Ark.Tools.EventSourcing.Aggregates
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class AggregateNameAttribute : Attribute
    {
        public string Name { get; }

        public AggregateNameAttribute(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EventNameAttribute : Attribute
    {
        public string Name { get; }

        public EventNameAttribute(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

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

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AggregateNameAttribute : Attribute
    {
        public string Name { get; }

        public AggregateNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EventNameAttribute : Attribute
    {
        public string Name { get; }

        public EventNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EventVersionAttribute : Attribute
    {
        public int Version { get; }

        public EventVersionAttribute(int version)
        {
            Version = version;
        }
=======
namespace Ark.Tools.EventSourcing.Aggregates;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AggregateNameAttribute : Attribute
{
    public string Name { get; }

    public AggregateNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventNameAttribute : Attribute
{
    public string Name { get; }

    public EventNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventVersionAttribute : Attribute
{
    public int Version { get; }

    public EventVersionAttribute(int version)
    {
        Version = version;
>>>>>>> After


namespace Ark.Tools.EventSourcing.Aggregates;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AggregateNameAttribute : Attribute
{
    public string Name { get; }

    public AggregateNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventNameAttribute : Attribute
{
    public string Name { get; }

    public EventNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventVersionAttribute : Attribute
{
    public int Version { get; }

    public EventVersionAttribute(int version)
    {
        Version = version;
    }
}
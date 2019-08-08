using System;
using System.Linq;
using System.Reflection;

namespace Ark.Tools.EventSourcing.Aggregates
{
    public static class AggregateHelper<TAggregate>
        where TAggregate : IAggregate
    {
        public static string Name { get; } = _getName();

        public static class EventHelper<TEvent>
            where TEvent : IAggregateEvent<TAggregate>
        {
            public static string Name { get; } = _getName();
            private static string _getName() 
                => typeof(TEvent)
                .GetTypeInfo()
                .GetCustomAttributes<EventNameAttribute>().SingleOrDefault()?.Name 
                ?? typeof(TEvent).Name;
        }

        public static string EventName(Type t)
        {
            if (!typeof(IAggregateEvent<TAggregate>).IsAssignableFrom(t))
                throw new ArgumentException("is not an event for this aggregate", nameof(t));

            var helperType = typeof(EventHelper<>).MakeGenericType(t);
            var prop = helperType.GetProperty("Name");
            return (string)prop.GetValue(null, null);
        }

        private static string _getName()
        {
            return typeof(TAggregate)
                .GetTypeInfo()
                .GetCustomAttributes<AggregateNameAttribute>().SingleOrDefault()?.Name
                ?? typeof(TAggregate).Name;
        }
    }

    public static class AggregateHelper
    {
        public static string AggregateName(Type t)
        {
            if (!typeof(IAggregateRoot).IsAssignableFrom(t))
                throw new ArgumentException("is not an aggregate root", nameof(t));

            var helperType = typeof(AggregateHelper<>).MakeGenericType(t);
            var prop = helperType.GetProperty("Name");
            return (string)prop.GetValue(null, null);
        }
    }
}

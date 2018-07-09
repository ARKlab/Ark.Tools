using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ark.Tools.Core
{
    public static class ReflectionHelper
    {
        /// <summary>
        /// Get the `Attribute` object of the specified type associated with a member.
        /// </summary>
        /// <typeparam name="TAttribute">Type of attribute to get.</typeparam>
        /// <param name="memberInfo">The member to look for the attribute on.</param>
        public static TAttribute GetAttribute<TAttribute>(MemberInfo memberInfo)
        {
            var attributes = from a in memberInfo.GetCustomAttributes(true)
                             where a is TAttribute
                             select a;

            return (TAttribute)attributes.FirstOrDefault();
        }

        /// <summary>
        /// Get the `Attribute` object of the specified type associated with a class.
        /// </summary>
        /// <typeparam name="TAttribute">Type of attribute to get.</typeparam>
        /// <param name="type">The class to look for the attribute on.</param>
        public static TAttribute GetAttribute<TAttribute>(Type type)
        {
            var attributes = from a in type.GetCustomAttributes(true)
                             where a is TAttribute
                             select a;

            return (TAttribute)attributes.FirstOrDefault();
        }


        /// <summary>
        /// Get the item type of a type that implements `IEnumerable`.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetEnumerableItemType(Type type)
        {
            // If the type passed IS the interface type, success!
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            // Otherwise, loop through the interfaces until we find IEnumerable (if it exists).
            Type[] interfaces = type.GetInterfaces();
            foreach (Type i in interfaces)
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return i.GetGenericArguments()[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Get a field or property value from an object.
        /// </summary>
        /// <param name="obj">The object whose property we want.</param>
        /// <param name="name">The name of the field or property we want.</param>
        public static object GetFieldOrPropertyValue(object obj, string name)
        {
            if (obj == null) return null;

            var type = obj.GetType();
            var member = type.GetField(name) ?? type.GetProperty(name) as MemberInfo;

            if (member == null) return null;

            object value;

            switch (member.MemberType)
            {
                case MemberTypes.Property:
                    value = ((PropertyInfo)member).GetValue(obj, null);
                    break;
                case MemberTypes.Field:
                    value = ((FieldInfo)member).GetValue(obj);
                    break;
                default:
                    value = null;
                    break;
            }

            return value;
        }

        /// <summary>
        /// Get a field or property value from an object.
        /// </summary>
        /// <param name="obj">The object whose property we want.</param>
        /// <param name="name">The name of the field or property we want.</param>
        public static T GetFieldOrPropertyValue<T>(object obj, string name)
        {
            if (obj == null) return default(T);

            var type = obj.GetType();
            var member = type.GetField(name) ?? type.GetProperty(name) as MemberInfo;

            if (member == null) return default(T);

            var value = GetFieldOrPropertyValue(obj, name);

            return (T)value;
        }
    }
}

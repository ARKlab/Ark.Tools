﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ark.Tools.Core
{
    /// <summary>
    /// Reflection utilities
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Get the `Attribute` object of the specified type associated with a member.
        /// </summary>
        /// <typeparam name="TAttribute">Type of attribute to get.</typeparam>
        /// <param name="memberInfo">The member to look for the attribute on.</param>
        public static TAttribute? GetAttribute<TAttribute>(MemberInfo memberInfo)
        {
            var attributes = from a in memberInfo.GetCustomAttributes(true)
                             where a is TAttribute
                             select a;

            return attributes.Cast<TAttribute>().FirstOrDefault();
        }

        /// <summary>
        /// Get the `Attribute` object of the specified type associated with a class.
        /// </summary>
        /// <typeparam name="TAttribute">Type of attribute to get.</typeparam>
        /// <param name="type">The class to look for the attribute on.</param>
        public static TAttribute? GetAttribute<TAttribute>(Type type)
        {
            var attributes = from a in type.GetCustomAttributes(true)
                             where a is TAttribute
                             select a;

            return attributes.Cast<TAttribute>().FirstOrDefault();
        }

        public static Type? GetCompatibleGenericBaseClass(this Type? type, Type baseType)
        {
            Type? baseTypeToCheck = type;

            while (baseTypeToCheck != null && baseTypeToCheck != typeof(object))
            {
                if (baseTypeToCheck.IsGenericType)
                {
                    Type genericTypeToCheck = baseTypeToCheck.GetGenericTypeDefinition();
                    if (genericTypeToCheck == baseType)
                    {
                        return baseTypeToCheck;
                    }
                }

                baseTypeToCheck = baseTypeToCheck.BaseType;
            }

            return null;
        }

        public static Type? GetCompatibleGenericInterface(this Type? type, Type interfaceType)
        {
            if (type == null) return null;

            Type interfaceToCheck = type;

            if (interfaceToCheck.IsGenericType)
            {
                interfaceToCheck = interfaceToCheck.GetGenericTypeDefinition();
            }

            if (interfaceToCheck == interfaceType)
            {
                return type;
            }

            foreach (Type typeToCheck in type.GetInterfaces())
            {
                if (typeToCheck.IsGenericType)
                {
                    Type genericInterfaceToCheck = typeToCheck.GetGenericTypeDefinition();
                    if (genericInterfaceToCheck == interfaceType)
                    {
                        return typeToCheck;
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Get the item type of a type that implements `IEnumerable`.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type? GetEnumerableItemType(Type type)
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
        public static object? GetFieldOrPropertyValue(object? obj, string name)
        {
            if (obj == null) return null;

            var type = obj.GetType();
            var member = type.GetField(name) ?? type.GetProperty(name) as MemberInfo;

            if (member == null) return null;

            object? value;

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
        public static T? GetFieldOrPropertyValue<T>(object? obj, string name)
        {
            if (obj == null) return default;

            var type = obj.GetType();
            var member = type.GetField(name) ?? type.GetProperty(name) as MemberInfo;

            if (member == null) return default;

            var value = GetFieldOrPropertyValue(obj, name);

            return (T?)value;
        }

        /// <summary>
        /// Return the name of the type as it is written in CS code
        /// </summary>
        /// <param name="type">the type</param>
        /// <returns></returns>
        public static string GetCSTypeName(this Type type)
        {
            if (type == typeof(string))
            {
                return "string";
            }
            else if (type == typeof(object)) { return "object"; }
            else if (type == typeof(bool)) { return "bool"; }
            else if (type == typeof(char)) { return "char"; }
            else if (type == typeof(int)) { return "int"; }
            else if (type == typeof(float)) { return "float"; }
            else if (type == typeof(double)) { return "double"; }
            else if (type == typeof(long)) { return "long"; }
            else if (type == typeof(ulong)) { return "ulong"; }
            else if (type == typeof(uint)) { return "uint"; }
            else if (type == typeof(byte)) { return "byte"; }
            else if (type == typeof(Int64)) { return "Int64"; }
            else if (type == typeof(short)) { return "short"; }
            else if (type == typeof(decimal)) { return "decimal"; }
            else if (type.IsGenericType)
            {
                return _toGenericTypeString(type);
            }
            else if (type.IsArray)
            {
                List<string> arrayLength = new List<string>();
                for (int i = 0; i < type.GetArrayRank(); i++)
                {
                    arrayLength.Add("[]");
                }
                return GetCSTypeName(type.GetElementType()!) + string.Join(string.Empty, arrayLength).Replace('+', '.');
            }
            else if (type.IsGenericParameter)
            {
                return type.Name;
            }
            else
            {
                return (type.FullName ?? type.Name).Replace('+', '.');
            }
        }

        private static string _toCSReservatedWord(this Type type, bool fullName)
        {
            if (type == typeof(string))
            {
                return "string";
            }
            else if (type == typeof(object)) { return "object"; }
            else if (type == typeof(bool)) { return "bool"; }
            else if (type == typeof(char)) { return "char"; }
            else if (type == typeof(int)) { return "int"; }
            else if (type == typeof(float)) { return "float"; }
            else if (type == typeof(double)) { return "double"; }
            else if (type == typeof(long)) { return "long"; }
            else if (type == typeof(ulong)) { return "ulong"; }
            else if (type == typeof(uint)) { return "uint"; }
            else if (type == typeof(byte)) { return "byte"; }
            else if (type == typeof(Int64)) { return "Int64"; }
            else if (type == typeof(short)) { return "short"; }
            else if (type == typeof(decimal)) { return "decimal"; }
            else
            {
                if (fullName && type.FullName is not null)
                {
                    return type.FullName;
                }
                else
                {
                    return type.Name;
                }
            }
        }

        private static string _toGenericTypeString(this Type t, params Type[] arg)
        {
            if (t.IsGenericParameter || t.FullName == null) return t.Name; //Generic argument stub
            bool isGeneric = t.IsGenericType || t.FullName.IndexOf('`') >= 0;//an array of generic types is not considered a generic type although it still have the genetic notation
            bool isArray = !t.IsGenericType && t.FullName.IndexOf('`') >= 0;
            Type genericType = t;
            while (genericType.IsNested && genericType.DeclaringType?.GetGenericArguments().Count() == t.GetGenericArguments().Count())//Non generic class in a generic class is also considered in Type as being generic
            {
                genericType = genericType.DeclaringType;
            }
            if (!isGeneric) return _toCSReservatedWord(t, true).Replace('+', '.');

            var arguments = arg.Any() ? arg : t.GetGenericArguments();//if arg has any then we are in the recursive part, note that we always must take arguments from t, since only t (the last one) will actually have the constructed type arguments and all others will just contain the generic parameters
            string genericTypeName = genericType._toCSReservatedWord(true);
            if (genericType.IsNested)
            {
                var argumentsToPass = arguments.Take(genericType.DeclaringType?.GetGenericArguments().Count()??0).ToArray();//Only the innermost will return the actual object and only from the GetGenericArguments directly on the type, not on the on genericDfintion, and only when all parameters including of the innermost are set
                arguments = arguments.Skip(argumentsToPass.Count()).ToArray();
                genericTypeName = genericType.DeclaringType?._toGenericTypeString(argumentsToPass) + "." + _toCSReservatedWord(genericType, false);//Recursive
            }
            if (isArray)
            {
                genericTypeName = t.GetElementType()?._toGenericTypeString() + "[]";//this should work even for multidimensional arrays
            }
            if (genericTypeName.IndexOf('`') >= 0)
            {
                genericTypeName = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));
                string genericArgs = string.Join(", ", arguments.Select(a => a._toGenericTypeString()).ToArray());
                //Recursive
                genericTypeName = genericTypeName + "<" + genericArgs + ">";
                if (isArray) genericTypeName += "[]";
            }
            if (t != genericType)
            {
                genericTypeName += t.FullName.Replace(genericType._toCSReservatedWord(true), "").Replace('+', '.');
            }
            if (genericTypeName.IndexOf("[") >= 0 && genericTypeName.IndexOf("]") != genericTypeName.IndexOf("[") + 1) 
                genericTypeName = genericTypeName.Substring(0, genericTypeName.IndexOf("["));//For a non generic class nested in a generic class we will still have the type parameters at the end 
            return genericTypeName;
        }
    }
}

// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;

namespace Ark.Tools.Core;

/// <summary>
/// Reflection helper methods that are trim-safe with proper annotations.
/// For more complex reflection utilities, see Ark.Tools.Reflection package.
/// </summary>
public static class ReflectionHelper
{
    /// <summary>
    /// Get the `Attribute` object of the specified type associated with a member.
    /// </summary>
    /// <typeparam name="TAttribute">Type of attribute to get.</typeparam>
    /// <param name="memberInfo">The member to look for the attribute on.</param>
    /// <returns>The first matching attribute, or null if not found.</returns>
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
    /// <returns>The first matching attribute, or null if not found.</returns>
    /// <remarks>
    /// This method uses GetCustomAttributes on the Type itself. The trimmer preserves
    /// type-level attributes by default, so no DynamicallyAccessedMembers annotation is needed.
    /// </remarks>
    public static TAttribute? GetAttribute<TAttribute>(Type type)
    {
        var attributes = from a in type.GetCustomAttributes(true)
                         where a is TAttribute
                         select a;

        return attributes.Cast<TAttribute>().FirstOrDefault();
    }

    /// <summary>
    /// Gets the compatible generic base class that matches the specified base type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="baseType">The base type to match (e.g., typeof(Dictionary&lt;,&gt;)).</param>
    /// <returns>The matching generic base class type, or null if not found.</returns>
    /// <remarks>
    /// This method is trim-safe because it only uses BaseType and GetGenericTypeDefinition(),
    /// which do not require DynamicallyAccessedMembers annotations.
    /// </remarks>
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

    /// <summary>
    /// Gets the compatible generic interface that matches the specified interface type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="interfaceType">The interface type to match (e.g., typeof(IDictionary&lt;,&gt;)).</param>
    /// <returns>The matching generic interface type, or null if not found.</returns>
    /// <remarks>
    /// This method requires DynamicallyAccessedMembers.Interfaces annotation to be trim-safe
    /// because it calls type.GetInterfaces(). The annotation ensures the trimmer preserves
    /// interface metadata for the type parameter.
    /// </remarks>
    public static Type? GetCompatibleGenericInterface(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type? type,
        Type interfaceType)
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
}

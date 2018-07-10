// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Ark.Tools.AspNetCore.XlsxOutputFormatter.Attributes;
using Ark.Tools.Core;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter
{
    public static class FormatterUtils
    {

        const BindingFlags PublicInstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public;


        /// <summary>
        /// Get the value of the `ExcelAttribute.Order` attribute associated with a given
        /// member. If not found, will default to the `DataMember.Order` value.
        /// </summary>
        /// <param name="member">The member for which to find the `ExcelAttribute.Order` value.</param>
        public static Int32 MemberOrder(MemberInfo member)
        {
            var excelProperty = ReflectionHelper.GetAttribute<ExcelColumnAttribute>(member);
            if (excelProperty != null && excelProperty._order.HasValue)
                return excelProperty.Order;

            var dataMember = ReflectionHelper.GetAttribute<DataMemberAttribute>(member);
            if (dataMember != null)
                return dataMember.Order;

            return -1;
        }

        /// <summary>
        /// Get the value of the `ExcelAttribute.Ignore` attribute associated with a given
        /// member. If not found, will default to the `DataMember.Ignore` value.
        /// </summary>
        /// <param name="member">The member for which to find the `ExcelAttribute.Ignore` value.</param>
        public static bool IsMemberIgnored(MemberInfo member)
        {
            var excelProperty = ReflectionHelper.GetAttribute<ExcelColumnAttribute>(member);
            if (excelProperty != null)
                return excelProperty.Ignore;

            return false;
        }

        /// <summary>
        /// Get an ordered list of non-ignored public instance property names of a type.
        /// </summary>
        /// <param name="type">The type on which to look for members.</param>
        public static List<string> GetMemberNames(Type type)
        {
            var memberInfo =  type.GetProperties(PublicInstanceBindingFlags)
                                  .OfType<MemberInfo>()
                                  .Union(type.GetFields(PublicInstanceBindingFlags));

            var memberNames = from p in memberInfo
                              where !IsMemberIgnored(p)
                              orderby MemberOrder(p)
                              select p.Name;

            return memberNames.ToList();
        }

        /// <summary>
        /// Get an ordered list of <c>MemberInfo</c> for non-ignored public instance
        /// properties on the specified type.
        /// </summary>
        /// <param name="type">The type on which to look for members.</param>
        public static List<MemberInfo> GetMemberInfo(Type type)
        {
            var memberInfo = type.GetProperties(PublicInstanceBindingFlags)
                                 .OfType<MemberInfo>()
                                 .Union(type.GetFields(PublicInstanceBindingFlags));

            var orderedMemberInfo = from p in memberInfo
                                    where !IsMemberIgnored(p)
                                    orderby MemberOrder(p)
                                    select p;

            return orderedMemberInfo.ToList();
        }

        /// <summary>
        /// Determine whether a type is simple (<c>String</c>, <c>Decimal</c>, <c>DateTime</c> etc.)
        /// or complex (i.e. custom class with public properties and methods).
        /// </summary>
        /// <see cref="https://gist.github.com/jonathanconway/3330614"/>
        /// <see cref="http://stackoverflow.com/questions/2442534/how-to-test-if-type-is-primitive"/>
        public static bool IsSimpleType(Type type)
        {
            return
                type.IsValueType ||
                type.IsPrimitive ||
                new Type[] {
                    typeof(string),
                    typeof(decimal),
                    typeof(DateTime),
                    typeof(DateTimeOffset),
                    typeof(TimeSpan),
                    typeof(Guid)
                }.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object;
        }

    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Ark.Tools.EventSourcing
{
	public static class Ex
	{
		private static Dictionary<Type, string> _fullNameCache = new Dictionary<Type, string>();
		/// <summary>
		/// Gets the full name without version information.
		/// </summary>
		/// <param name="entityType">Type of the entity.</param>
		/// <returns></returns>
		public static string GetFullNameWithoutVersionInformation(Type entityType)
		{
			string result;
			var localFullName = _fullNameCache;
			if (localFullName.TryGetValue(entityType, out result))
				return result;

			var asmName = new AssemblyName(entityType.GetTypeInfo().Assembly.FullName).Name;
			if (entityType.GetTypeInfo().IsGenericType)
			{
				var genericTypeDefinition = entityType.GetGenericTypeDefinition();
				var sb = new StringBuilder(genericTypeDefinition.FullName);
				sb.Append("[");
				bool first = true;
				foreach (var genericArgument in entityType.GetGenericArguments())
				{
					if (first == false)
					{
						sb.Append(", ");
					}
					first = false;
					sb.Append("[")
						.Append(GetFullNameWithoutVersionInformation(genericArgument))
						.Append("]");
				}
				sb.Append("], ")
					.Append(asmName);
				result = sb.ToString();
			}
			else
			{
				result = entityType.FullName + ", " + asmName;
			}

			_fullNameCache = new Dictionary<Type, string>(localFullName)
			{
				{entityType, result}
			};

			return result;
		}

		public static bool IsAssignableFromEx(this Type baseType, Type extendType)
		{
			if (baseType.IsInterface)
			{
				var interfaces = extendType.GetInterfaces();
				foreach (var i in interfaces)
				{
					var l = i;
					if (baseType.IsGenericTypeDefinition && i.IsGenericType)
						l = l.GetGenericTypeDefinition();

					if (baseType.IsAssignableFrom(l))
						return true;
				}
				return false;
			}

			while (!baseType.IsAssignableFrom(extendType))
			{
				if (extendType.Equals(typeof(object)))
				{
					return false;
				}
				if (extendType.IsGenericType && !extendType.IsGenericTypeDefinition)
				{
					extendType = extendType.GetGenericTypeDefinition();
				}
				else
				{
					extendType = extendType.BaseType;
				}
			}
			return true;
		}
	}
}

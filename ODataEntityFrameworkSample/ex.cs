using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections;
using System.Reflection;

namespace ODataEntityFrameworkSample
{
	static class Ex
	{
		public static void CloneReflection<T>(this EntityEntry<T> target, EntityEntry<T> source)
			where T : class
		{
			object t = target.Entity;
			// Get all FieldInfo.
			_cloneRefl(ref t, source.Entity);
		}

		private static void _cloneRefl(ref Object target, Object source)
		{
			if (source == null)
			{
				target = null;
				return;
			}

			Type type = source.GetType();

			if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsValueType)
			{
				target = source;
				return;
			}
			else if (type.IsArray)
			{
				throw new NotSupportedException("array anche no");
			}
			else if (typeof(IList).IsAssignableFrom(type))
			{ 
				//NAVIGATIONS
				//KEYS OF NAVIGATIONS
				//OWNED ENTITIES
				//are NOT Supported at the moment
				// --> We cannot use EF!
				var arraySource = source as IList;
				var arrayTarget = target as IList;

				arrayTarget.Clear();
				foreach (var i in arraySource)
					arrayTarget.Add(i);
			}
			else if (type.IsClass)
			{
				// Get all FieldInfo.
				var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
				foreach (var field in fields)
				{
					var sourceValue = field.GetValue(source);
					var targetValue = field.GetValue(target);
					_cloneRefl(ref targetValue, sourceValue);
					field.SetValue(target, targetValue);
				}
				return;
			}
			else
			{
				throw new ArgumentException("The object is unknown type");
			}
		}
	}
}

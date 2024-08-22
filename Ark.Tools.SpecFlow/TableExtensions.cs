using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace Ark.Tools.SpecFlow
{
    public static class TableExtensions
    {
        public static T CreateComplexObject<T>(this Table table)
        {
            var s = table.CreateComplexSet<T>();
            return s.Single();
        }
        /// <summary>
        /// Converts a specflow table to an instance. But includes complex objects as well (ParentProperty.ChildProperty is the convention)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table"></param>
        /// <returns></returns>
        public static IEnumerable<T> CreateComplexSet<T>(this Table table)
        {
            var items = new List<T>();

            foreach (var row in table.Rows)
                items.Add(CreateComplexType<T>(row));

            return items;
        }

        public static T CreateComplexType<T>(this TableRow tableRow)
        {
            T result = tableRow.CreateInstance<T>();

            // find sub-properties by looking for "."
            var propNames = tableRow
                .Where(x => x.Key.Contains('.'))
                .Select(x => Regex.Replace(x.Key, @"^(.+?)\..+$", "$1"));

            foreach (var propName in propNames)
            {
                // look for matching property in result object
                var prop = typeof(T).GetProperty(
                    propName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (prop != null)
                {
                    var headers = new List<string>();
                    var values = new List<string>();

                    foreach (var item in tableRow)
                    {
                        if (item.Key.StartsWith($"{propName}.", StringComparison.InvariantCultureIgnoreCase))
                        {
                            headers.Add(item.Key.Replace($"{propName}.", "", StringComparison.InvariantCultureIgnoreCase));
                            values.Add(item.Value);
                        }
                    }

                    var subTable = new Table(headers.ToArray());
                    subTable.AddRow(values.ToArray());

                    // make recursive call to create child object
                    var createInstance = typeof(TableExtensions)
                        .GetMethod(
                            "CreateComplexObject",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            CallingConventions.Any,
                            new Type[] { typeof(Table) },
                            null);
                    createInstance = createInstance?.MakeGenericMethod(prop.PropertyType);
                    object? propValue = createInstance?.Invoke(null, new object[] { subTable });

                    prop.SetValue(result, propValue);
                }
            }

            return result;
        }

        /// <summary>
        /// Verifies that all properties specified in the table exist on the target type and returns a complex set if they do.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table"></param>
        /// <returns></returns>
        public static IEnumerable<T> VerifyPropertiesAndCreateSet<T>(this Table table)
        {
            table.VerifyAllPropertiesExistOnTargetType<T>();
            return table.CreateComplexSet<T>();
        }

        /// <summary>
        /// Verifies that all headings specified in the table exist as properties in the target type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table"></param>
        public static void VerifyAllPropertiesExistOnTargetType<T>(this Table table)
        {
            var headers = table.Header.Select(e => e.Split('.').First());
            var propertiesOnTargetType = typeof(T).GetProperties().Select(e => e.Name).ToList();

            var missingProperties = headers.Where(e => !propertiesOnTargetType.Contains(e)).ToList();

            if (missingProperties.Any())
            {
                var message = $"Type {typeof(T).Name} is missing the following properties specifield in the table: {string.Join(", ", missingProperties)}.";
                throw new ArgumentException(message);
            }
        }
    }
}

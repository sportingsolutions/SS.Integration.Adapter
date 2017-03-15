using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SS.Integration.Common.Converters
{
    public static class ReflectionCsvSerializer
    {
        public static void Serialize<T>(IEnumerable<T> items, string fileName)
        {
            var lines = Serialize(items);
            if (lines != null)
                WriteLines(fileName, lines);
        }
        public static List<string> Serialize<T>(IEnumerable<T> items)
        {
            if (items == null || !items.Any(x => x != null))
                return null;
            List<string> csvLines = new List<string>();
            var props = typeof(T).GetProps();

            var enumerable = props.Select(x => x.Name);
            var propNames = enumerable.JoinToCsvLine();
            csvLines.Add(propNames);

            foreach (var item in items)
            {
                var propValues = GetObjectPropValues(props, item);
                csvLines.Add(propValues.JoinToCsvLine());
            }
            return csvLines;
        }
        private static PropertyInfo[] GetProps(this Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.CanRead)
                    .ToArray();
        }
        private static string[] GetObjectPropValues(PropertyInfo[] props, object item)
        {
            return props.Select(x =>
            {



                try
                {
                    var val = x.GetValue(item);
                    return val == null ? "null" : val.ToString();
                }
                catch (Exception e)
                {
                    return "null";
                }


            }).ToArray();
        }
        static void WriteLines(string fileName, List<string> sList)

        {
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));


            using (StreamWriter r = new StreamWriter(fileName, true, Encoding.UTF8))
            {


                foreach (var value in sList)
                {
                    r.WriteLine(value);
                }
                r.Flush();
            }
        }
        static string JoinToCsvLine(this IEnumerable<string> items)
        {
            return "\"" + string.Join("\",\"", items) + "\"";
        }

    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SS.Integration.Common.Extensions
{
    public static class CommonExtensions
    {
        public static T FromXElement<T>(this XElement xElement)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            return (T)xmlSerializer.Deserialize(xElement.CreateReader());
        }
        public static decimal ToDecimalInvariantCulture(this string input)
        {

            string output = input.Trim().Replace(" ", "").Replace(",", ".");
            string[] split = output.Split('.');
            if (split.Count() > 1)
            {
                output = string.Join("", split.Take(split.Count() - 1).ToArray());
                output = string.Format("{0}.{1}", output, split.Last());
            }

            return decimal.Parse(output, CultureInfo.InvariantCulture);
        }

        public static bool EqualsOrdinalIgnoreCase(this string str, string str2)
        {
            return string.Equals(str, str2, StringComparison.OrdinalIgnoreCase);
        }
    }
}

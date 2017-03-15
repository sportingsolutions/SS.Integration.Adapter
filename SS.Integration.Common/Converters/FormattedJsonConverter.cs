using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SS.Integration.Common.Converters
{
   public static class FormattedJsonConverter
    {
        /// <summary>
        /// Using  FixtureDateTimeJsonConverter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content"></param>
        /// <returns></returns>
        public static T FromJson<T>(string content)
        {
            return JsonConvert.DeserializeObject<T>(content, FixtureDateTimeJsonConverter.Instance);
        }
        /// <summary>
        /// Using  FixtureDateTimeJsonConverter
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static string ToJson(this object o)
        {
            return JsonConvert.SerializeObject(o, FixtureDateTimeJsonConverter.Instance);
        }
    }
}

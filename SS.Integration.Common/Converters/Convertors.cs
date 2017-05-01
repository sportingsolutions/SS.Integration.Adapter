using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.Converters
{
    public static class Convertors
    {
        public static bool BoolFromString(this string value)
        {
            return !string.IsNullOrEmpty(value) && Convert.ToBoolean(value);
        }


        public static int IntFromString(this string value, int def = 0)
        {
            if (!string.IsNullOrEmpty(value))
            {
                int result;
                if (int.TryParse(value, out result) && result > 0)
                    return result;
            }

            return def;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Pattern;

namespace SS.Integration.Common.Extensions
{
    public static class TimeExtensions
    {
        //public static TimeSpan RetryInterval(this int attempt)
        //{
        //    var second = (int) Math.Pow(2, attempt-1);
        //    return TimeSpan.FromSeconds(second);
        //}


        public static TimeSpan RetryInterval(this int attempt, int startInterval = 1)
        {
            var second = (int)Math.Pow(2, attempt - 1);
            return TimeSpan.FromSeconds(startInterval * second);
        }

	    public static string ToStringSec(this TimeSpan time)
	    {
		    var second = time.TotalSeconds;
		    var d = decimal.Round((Decimal) second, 3);
		    return d.ToString();
	    }

		public static string TimeString(this TimeSpan? time, string name, bool ignoreZero = false)
	    {
		    return time.HasValue
			    ? time.Value.TimeString(name, ignoreZero)
			    : string.Empty;
	    }

	    public static string TimeString(this TimeSpan time, string name, bool ignoreZero = false)
	    {
		    return !ignoreZero || time.TotalSeconds > 0.0005f 
			    ? $"{name}Time={time.ToStringSec()}" 
			    : "";

	    }
	}


}

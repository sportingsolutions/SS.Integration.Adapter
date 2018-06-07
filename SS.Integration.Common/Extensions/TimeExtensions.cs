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
    }
}

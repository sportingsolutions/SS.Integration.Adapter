using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.Extensions
{
    public class Anonymous
    {
        //this method shouldn't be used to instantiate any other dictionary as it can be achievied through normal constructor
        public static IDictionary<TKey, TValue> CreateDictionaryWithAnonymousObject<TKey,TValue>(TKey key, TValue value)
        {
            return new Dictionary<TKey, TValue>();
        }

    }
}

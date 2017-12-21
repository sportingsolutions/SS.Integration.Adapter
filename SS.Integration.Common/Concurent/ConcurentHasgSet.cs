using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.Concurent
{
    public class ConcurentHashSet <T>
    {
        private readonly ConcurrentDictionary<T, bool> _dictionary = new ConcurrentDictionary<T, bool>();


        public bool TryAdd(T key) => _dictionary.TryAdd(key, true);
        public List<T> ToList() => _dictionary.Keys.ToList();

        public int Count => _dictionary.Count;

        public bool TryRemove(T key)
        {
            bool tmp;
            return _dictionary.TryRemove(key, out tmp);
        }

        public bool Contains(T key)
        {
            bool tmp;
            return _dictionary.TryGetValue(key, out tmp);
        }


    }
}

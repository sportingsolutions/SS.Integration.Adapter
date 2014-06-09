//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Runtime.Caching;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Model
{
    public class CachedObjectStore<T> : IObjectProvider<T> where T:class
    {
        private readonly string _cacheUniqueId;
        private MemoryCache _cache;
        private readonly CacheItemPolicy _cacheItemPolicy;
        private static readonly object _sync = new object();

        public CachedObjectStore(string cacheUniqueId,int cacheExpiryInSecs = 60)
        {
            _cacheUniqueId = cacheUniqueId;
            _cache = new MemoryCache(cacheUniqueId);
            _cacheItemPolicy = new CacheItemPolicy() {SlidingExpiration = TimeSpan.FromSeconds(cacheExpiryInSecs)};
        }

        public virtual T GetObject(string id)
        {
            return _cache.Get(id) as T;
        }

        public virtual void SetObject(string id,T item)
        {
            var cacheItem = new CacheItem(id, item);
            _cache.Set(cacheItem,_cacheItemPolicy);
        }

        public virtual bool Remove(string id)
        {
            _cache.Remove(id);
            return true;
        }
        
        public void Clear(string id = null)
        {
            lock (_sync)
            {
                _cache.Dispose();
                _cache = new MemoryCache(_cacheUniqueId);
            }
        }

        public void Flush()
        {
            
        }
    }
}

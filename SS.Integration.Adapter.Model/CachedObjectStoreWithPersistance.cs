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
using SS.Integration.Adapter.Model.Interfaces;
using log4net;

namespace SS.Integration.Adapter.Model
{
    public class CachedObjectStoreWithPersistance<T> : CachedObjectStore<T> where T : class
    {
        private readonly IObjectProvider<T> _persistanceStore;
        private readonly ILog _logger = LogManager.GetLogger(typeof (CachedObjectStoreWithPersistance<T>));

        public CachedObjectStoreWithPersistance(IObjectProvider<T> persistanceStore, string cacheUniqueId,
                                                int cacheExpiryInSecs = 60)
            : base(cacheUniqueId, cacheExpiryInSecs)
        {
            _persistanceStore = persistanceStore;
        }

        public override T GetObject(string id)
        {
            try
            {
                var item = base.GetObject(id);
                var notInCache = item == null;

                if(notInCache)
                    item = _persistanceStore.GetObject(id);

                if (notInCache && item != null)
                {
                    base.SetObject(id,item);
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Deserialization error occured for item with id={0}. Storage file must be corrupted and will be removed",id), ex);
                
                Remove(id);
                
            }

            return null;
        }

        public override bool Remove(string id)
        {
            _persistanceStore.Remove(id);
            base.Remove(id);
            return true;
        }

        public override void SetObject(string id, T item)
        {
            base.SetObject(id, item);
            _persistanceStore.SetObject(id, item);
        }
    }
}

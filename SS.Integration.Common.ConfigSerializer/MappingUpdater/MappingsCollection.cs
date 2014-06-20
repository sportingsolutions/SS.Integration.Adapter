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
using System.Collections.Concurrent;
using System.Collections.Generic;
using SS.Integration.Common.ConfigSerializer.MappingUpdater.Interfaces;


namespace SS.Integration.Common.ConfigSerializer.MappingUpdater
{
    public class MappingsCollection<T> : IObserver<IEnumerable<T>>, IMappingsCollection<T>
    {

        #region Constructors

        public MappingsCollection()
        {
        }

        public MappingsCollection(IDictionary<string,T> dictionary,Func<T,string> keySelector)
        {
            if (keySelector == null)
            {
                throw new ArgumentNullException("keySelector");
            }
            KeySelector = keySelector;
            this.Collection = new ConcurrentDictionary<string, T>(dictionary);
        }

        #endregion

        #region Properties

        public Func<T, string> KeySelector { get; private set; }

        private ConcurrentDictionary<string, T> _collection = new ConcurrentDictionary<string, T>();
        private ConcurrentDictionary<string, T> Collection
        {
            get { return _collection; }
            set { _collection = value; }
        }

        public T this[string key]
        {
            get
            {
                if (this.Collection == null)
                    return default(T);

                if (this.Collection.ContainsKey(key))
                    return this.Collection[key];
                return default(T);
            }
        }

        public int Count
        {
            get
            {
                if (this.Collection == null)
                    return 0;
                return this.Collection.Count;
            }
        }



        #endregion

        #region IObserverImplementation

        public void OnCompleted()
        {
            //do nothing
        }

        public void OnError(Exception error)
        {
            //do nothing
        }

        public void OnNext(IEnumerable<T> value)
        {
 
            foreach (T mapping in value)
            {
                this.Collection.AddOrUpdate(KeySelector(mapping), mapping,
                    (key, existingVal) =>
                    {
                        return mapping;
                    });
            }
            
        }

        #endregion

    }
}

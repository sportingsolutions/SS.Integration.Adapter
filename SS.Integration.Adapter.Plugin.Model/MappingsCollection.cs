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

using SS.Integration.Adapter.Plugin.Model.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace SS.Integration.Adapter.Plugin.Model
{
    public class MappingsCollection : IObserver<IEnumerable<Mapping>>, IMappingsCollection
    {

        #region Constructors

        public MappingsCollection()
        {
        }

        public MappingsCollection(IDictionary<string,Mapping> dictionary)
        {
            this.Collection = new ConcurrentDictionary<string, Mapping>(dictionary);
        }

        #endregion

        #region Properties

        private ConcurrentDictionary<string, Mapping> _collection = new ConcurrentDictionary<string, Mapping>();
        private ConcurrentDictionary<string, Mapping> Collection
        {
            get { return _collection; }
            set { _collection = value; }
        }

        public Mapping this[string key]
        {
            get
            {
                if (this.Collection == null)
                    return null;

                if (this.Collection.ContainsKey(key))
                    return this.Collection[key];
                return null;
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
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(IEnumerable<Mapping> value)
        {
 
            foreach (Mapping mapping in value)
            {
                this.Collection.AddOrUpdate(mapping.Sport, mapping,
                    (key, existingVal) =>
                    {
                        return mapping;
                    });
            }
            
        }

        #endregion

    }
}

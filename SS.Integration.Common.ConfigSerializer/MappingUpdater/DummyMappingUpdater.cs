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


﻿using System;
using System.Collections.Generic;
﻿using SS.Integration.Common.ConfigSerializer.MappingUpdater.Interfaces;

namespace SS.Integration.Common.ConfigSerializer.MappingUpdater
{
    public class DummyMappingUpdater<T> : IMappingUpdater<T>
    {

        public string FileNameOrReference { get; set; }

        public ISportConfigSerializer Serializer { get; set; }

        private List<IObserver<IEnumerable<T>>> _observers;
        public List<IObserver<IEnumerable<T>>> Observers
        {
            get
            {
                if (_observers == null)
                {
                    _observers = new List<IObserver<IEnumerable<T>>>();
                }
                return _observers;
            }

        }

        public IEnumerable<T> LoadMappings()
        {
            return null;
        }

        public void NotifySubscribers(IEnumerable<T> mappings)
        {

        }

        public void Initialize()
        {

        }

        public void Dispose()
        {

        }
    }
}
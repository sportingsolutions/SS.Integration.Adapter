﻿using System;
using System.Collections.Generic;
﻿using SS.Integration.Adapter.Plugin.Model;
﻿using SS.Integration.Adapter.Plugin.Model.Interface;
using SS.Integration.Common.ConfigSerializer;

namespace SS.Integration.Adapter.Mappings
{
    public class DummyMappingUpdater : IMappingUpdater
    {

        public string FileNameOrReference { get; set; }

        public ISportConfigSerializer Serializer { get; set; }

        private List<IObserver<IEnumerable<Mapping>>> _observers;
        public List<IObserver<IEnumerable<Mapping>>> Observers
        {
            get
            {
                if (_observers == null)
                {
                    _observers = new List<IObserver<IEnumerable<Mapping>>>();
                }
                return _observers;
            }

        }

        public IEnumerable<Mapping> LoadMappings()
        {
            return null;
        }

        public void NotifySubscribers(IEnumerable<Mapping> mappings)
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
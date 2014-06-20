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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SS.Integration.Common.ConfigSerializer.MappingUpdater.Interfaces;
using log4net;

namespace SS.Integration.Common.ConfigSerializer.MappingUpdater
{
    public abstract class DefaultMappingUpdater<T> : IMappingUpdater<T>
    {

        protected  string CachedFileId = "Mappings";

        public ISportConfigSerializer Serializer { get; set; }
        public ICachedMappingsStorage<T> CachedMappingsStorage { get; set; }

        private ILog _logger = LogManager.GetLogger(typeof(DefaultMappingUpdater<T>));

        private string[] _sportsList;
        private string[] SportsList
        {
            get
            {
                if (_sportsList == null)
                    _sportsList = this.Serializer.GetSportsList("");
                return _sportsList;
            }

        }


        private int _checkForUpdatesInterval = 60000;
        public int CheckForUpdatesInterval
        {
            get { return _checkForUpdatesInterval; }
            set { _checkForUpdatesInterval = value; }
        }

        private Timer _trigger;

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

        protected DefaultMappingUpdater(ISportConfigSerializer serializer,
                                          ICachedMappingsStorage<T> cachedMappingsStorage)
        {
            this.Serializer = serializer;
            this.CachedMappingsStorage = cachedMappingsStorage;
        }

        private void ReplaceSingleMappingsInCache(List<T> newMapping)
        {
            CachedMappingsStorage.SaveMappingsInCache(newMapping);
        }

        public void Initialize()
        {
            //load initial mappings from cache (if exists)
            _logger.DebugFormat("looking for cached mappings");
            IEnumerable<T> cachedMappings = CachedMappingsStorage.GetMappingsInCache();
            if (cachedMappings == null)
            {
                _logger.DebugFormat("cached mappings not found");
                cachedMappings = LoadMappings();
            }
            else
            {
                _logger.DebugFormat("cached mappings loaded");
            }

            NotifySubscribers(cachedMappings);

            _trigger = new Timer(timerAutoEvent => CheckForUpdates(), null, 0, this.CheckForUpdatesInterval);
        }

        public void Dispose()
        {
            this.Observers.Clear();
            if (_trigger != null)
                _trigger.Dispose();
        }

        protected virtual T LoadMappings(string sport)
        {
            /*
            Mapping mapping = new Mapping();

            mapping.Sport = sport;
            _logger.DebugFormat("loading competition mappings for {0}", sport);

            mapping.CompetitionMappings = this.Serializer.Deserialize<CompetitionMapping>(MappingCategory.CompetitionMapping.ToString(),
                                                                                          sport);
            _logger.DebugFormat("competition mappings for {0} successfully loaded.", sport);

            _logger.DebugFormat("loading market mappings for {0}", sport);

            mapping.MarketsMapping = this.Serializer.Deserialize<MarketMapping>(MappingCategory.MarketMapping.ToString(),
                                                                                sport);

            _logger.DebugFormat("market mappings for {0} successfully loaded.", sport);

            return mapping;
            */
            return default(T);
        }
        
        public IEnumerable<T> LoadMappings()
        {

            List<T> newMappings = new List<T>();

            foreach (string sport in this.SportsList)
            {
                try
                {
                    T singleMapping = LoadMappings(sport);
                    newMappings.Add(singleMapping);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("error while loading mappings for {0}: {1}", sport, ex);
                }
                
            }

            ReplaceSingleMappingsInCache(newMappings);

            return newMappings;
        }

        public void NotifySubscribers(IEnumerable<T> mappings)
        {
            foreach (IObserver<IEnumerable<T>> observer in this.Observers)
            {
                try
                {
                    observer.OnNext(mappings);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("error while passing the mappings to the subscriber {0} : {1}", observer, ex);
                    observer.OnError(ex);
                }
                
            }


        }

        public void CheckForUpdates()
        {

            List<T> newMappings = new List<T>();
            List<string> sportProcessed = new List<string>();
            foreach (string sport in this.SportsList)
            {
                try
                {
                    _logger.DebugFormat("checking for updates sport={0}",sport);

                    if (Serializer.IsUpdateNeeded(MappingCategory.CompetitionMapping.ToString(),sport))
                    {
                        _logger.DebugFormat("new update found for sport={0}",sport);
                        T singleMap = LoadMappings(sport);
                        newMappings.Add(singleMap);
                        _logger.DebugFormat("mappings loaded for sport={0}", sport);
                        sportProcessed.Add(sport);
                    }
                    else
                    {
                        _logger.DebugFormat("no updates found for sport={0}", sport);
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("error while retrieving mappings for {0}: {1}",sport, ex);
                }
            }

            if (newMappings.Count() == 0)
            {
                _logger.DebugFormat("no updates found across all sports.");
            }
            else
            {
                string sportsNames = string.Join(",", sportProcessed);

                try
                {
                    _logger.DebugFormat("save mappings for {0} to cache", sportsNames);
                    ReplaceSingleMappingsInCache(newMappings);
                    _logger.DebugFormat("mappings for {0} saved to cache", sportsNames);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("error while saving mappings to cache : {0}",  ex);
                }

                _logger.DebugFormat("notification to subscribers");
                NotifySubscribers(newMappings);
                _logger.DebugFormat("subscribers notified");

            }
        }

    }
}

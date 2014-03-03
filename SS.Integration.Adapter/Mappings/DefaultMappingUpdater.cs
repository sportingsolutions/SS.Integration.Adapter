using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Adapter.Plugin.Model.Interface;
using SS.Integration.Common.ConfigSerializer;
using log4net;

namespace SS.Integration.Adapter.Mappings
{
    public class DefaultMappingUpdater : IMappingUpdater
    {
        private const string _cachedFileId = "Mappings";

        public ISportConfigSerializer Serializer { get; set; }
        public IObjectProvider<List<Mapping>> CachedObjectProvider { get; set; }

        public string FileNameOrReference { get; set; }

        private ILog _logger = LogManager.GetLogger(typeof(DefaultMappingUpdater));


        private int _checkForUpdatesInterval = 60000;
        public int CheckForUpdatesInterval
        {
            get { return _checkForUpdatesInterval; }
            set { _checkForUpdatesInterval = value; }
        }

        private string CompetitionFileName
        {
            get { return FileNameOrReference + "-Competitions"; }
        }

        private string SelectionsFileName
        {
            get { return FileNameOrReference + "-Selections"; }
        }

        private string MarketsFileName
        {
            get { return FileNameOrReference + "-Markets"; }
        }

        private Timer _trigger;

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

        private void ReplaceSingleMappingsInCache(List<Mapping> newMapping)
        {
            //load the current cached object
            List<Mapping> cachedMappings = this.CachedObjectProvider.GetObject(_cachedFileId);

            if (cachedMappings == null)
            {
                cachedMappings = new List<Mapping>();
            }
            else
            {
                //remove the mappings with the same sport
                cachedMappings.RemoveAll(map => newMapping.Exists(nm => nm.Sport == map.Sport));               
            }

            //add the mappings
            cachedMappings.AddRange(newMapping);

            //save to cache
            this.CachedObjectProvider.SetObject(_cachedFileId,cachedMappings);

        }

        public void Initialize()
        {
            //load initial mappings from cache (if exists)
            _logger.DebugFormat("looking for cached mappings");
            IEnumerable<Mapping> cachedMappings = this.CachedObjectProvider.GetObject(_cachedFileId);
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

        [Serializable]
        public class MarketMappingWithSelectionMapping : MarketMapping
        {
            public string SelectionMappingName { get; set; }
        }

        [Serializable]
        public class SelectionMappingWithName : SelectionMapping
        {
            public string MappingName { get; set; }
        }

        protected virtual Mapping LoadMappings(string sport)
        {
            _logger.DebugFormat("loading selection mappings for {0}", sport);
            List<SelectionMappingWithName> allSelectionMappings =
                this.Serializer.Deserialize<SelectionMappingWithName>(this.SelectionsFileName, sport);
            _logger.DebugFormat("selection mappings for {0} successfully loaded.", sport);

            Mapping mapping = new Mapping();

            mapping.Sport = sport;
            _logger.DebugFormat("loading competition mappings for {0}", sport);
            mapping.CompetitionMappings = this.Serializer.Deserialize<CompetitionMapping>(this.CompetitionFileName,
                                                                                          sport);
            _logger.DebugFormat("competition mappings for {0} successfully loaded.", sport);

            _logger.DebugFormat("loading market mappings for {0}", sport);
            List<MarketMappingWithSelectionMapping> tempList =
                this.Serializer.Deserialize<MarketMappingWithSelectionMapping>(this.MarketsFileName, sport);

            if (tempList != null)
            {
                mapping.MarketsMapping = tempList.Select(mm =>
                    {
                        if (allSelectionMappings != null)
                        {
                            mm.SelectionMappings =
                                allSelectionMappings
                                    .Where(sm => sm.MappingName == mm.SelectionMappingName)
                                    .Select(sm => (SelectionMapping) sm).ToList();
                        }
                        return (MarketMapping) mm;
                    }).ToList();
            }



            _logger.DebugFormat("market mappings for {0} successfully loaded.", sport);

            return mapping;
        }

        public IEnumerable<Mapping> LoadMappings()
        {
            List<Mapping> newMappings = new List<Mapping>();

            foreach (string sport in this.Serializer.GetSportsList(this.CompetitionFileName))
            {
                newMappings.Add(LoadMappings(sport));
            }

            this.CachedObjectProvider.SetObject(_cachedFileId,newMappings);

            return newMappings;
        }

        public void NotifySubscribers(IEnumerable<Mapping> mappings)
        {
            foreach (IObserver<IEnumerable<Mapping>> observer in this.Observers)
                observer.OnNext(mappings);
        }

        public void CheckForUpdates()
        {
            try
            {
                List<Mapping> newMappings = new List<Mapping>();

                foreach (string sport in this.Serializer.GetSportsList(this.CompetitionFileName))
                {
                    _logger.DebugFormat("checking for updates sport={0}",sport);

                    if (Serializer.IsUpdateNeeded(CompetitionFileName,sport))
                    {
                        _logger.DebugFormat("new update found for sport={0}",sport);
                        Mapping singleMap = LoadMappings(sport);
                        newMappings.Add(singleMap);
                        _logger.DebugFormat("mappings loaded for sport={0}", sport);
                    }
                    else
                    {
                        _logger.DebugFormat("no updates found for sport={0}", sport);
                    }
                }

                if (newMappings.Count() == 0)
                {
                    _logger.DebugFormat("no updates found across all sports.");
                }
                else
                {
                    string sportsNames = string.Join(",", newMappings.Select(it => it.Sport));

                    _logger.DebugFormat("save mappings for {0} to cache", sportsNames);
                    ReplaceSingleMappingsInCache(newMappings);
                    _logger.DebugFormat("mappings for {0} saved to cache", sportsNames);

                    _logger.DebugFormat("notification to subscribers");
                    NotifySubscribers(newMappings);
                    _logger.DebugFormat("subscribers notified"); 
                }

            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("error: {0}", ex);
            }
        }
    }
}

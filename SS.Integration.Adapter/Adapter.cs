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
using System.Reflection;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.ProcessState;
using SS.Integration.Adapter.UdapiClient.Model;
using log4net;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;

namespace SS.Integration.Adapter
{
    using Model.Enums;
    using System.Diagnostics;

    public class Adapter
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(Adapter).ToString());

        private readonly ConcurrentDictionary<string, IListener> _listeners;

        private readonly ISettings _settings;

        private readonly IServiceFacade _udapiServiceFacade;

        private readonly IAdapterPlugin _platformConnector;

        private readonly IEventState _eventState;

        private List<string> _sports;

        private readonly Func<string, IResourceFacade, Fixture, IAdapterPlugin, IEventState, IObjectProvider<IDictionary<string, MarketState>>, int, IListener> _createListener;

        private Timer _trigger;

        private readonly IObjectProvider<IDictionary<string, MarketState>> _marketStateObjectStore;

        private readonly BlockingCollection<IResourceFacade> _resourceCreationQueue;
        private readonly HashSet<string> _currentlyProcessedFixtures;
        private readonly static object _sync = new object();

        private readonly IStatsHandle _Stats;

        public Adapter(ISettings settings,
                       IServiceFacade udapiServiceFacade,
                       IAdapterPlugin platformConnector,
                       IEventState eventState,
                       Func<string, IResourceFacade, Fixture, IAdapterPlugin, IEventState, IObjectProvider<IDictionary<string, MarketState>>, int, IListener> listenerFactory)
        {
            _settings = settings;
            _udapiServiceFacade = udapiServiceFacade;
            _platformConnector = platformConnector;
            _eventState = eventState;
            _createListener = listenerFactory;
            _resourceCreationQueue = new BlockingCollection<IResourceFacade>(new ConcurrentQueue<IResourceFacade>());
            _currentlyProcessedFixtures = new HashSet<string>();

            // REFACTOR!
            //_marketStateObjectStore = new BinaryStoreProvider<IDictionary<string, MarketState>>(_settings.MarketFiltersDirectory,_settings.FilePathFormat);
            //
            _marketStateObjectStore = new CachedObjectStoreWithPersistance<IDictionary<string, MarketState>>(
                new BinaryStoreProvider<IDictionary<string, MarketState>>(_settings.MarketFiltersDirectory, "FilteredMarkets-{0}.bin"),
                "MarketFilters",
                settings.CacheExpiryInMins * 60
                );

            ThreadPool.SetMinThreads(500, 500);

            _sports = new List<string>();
            _Stats = StatsManager.Instance["Adapter"].GetHandle();
            _listeners = new ConcurrentDictionary<string, IListener>();
            _Stats.SetValue(AdapterKeys.STATUS, "Created");          
        }

        public void Start()
        {
            try
            {
                LogVersions();

                _udapiServiceFacade.Connect();

                _Stats.SetValue(AdapterKeys.STATUS, "Connected");

                _logger.Info("initialising trigger event ...");

                for (var i = 0; i < _settings.FixtureCreationConcurrency; i++)
                {
                    Task.Factory.StartNew(CreateFixture);
                }


                foreach (var sport in _udapiServiceFacade.GetSports())
                {
                    _sports.Add(sport.Name);
                }

                _trigger = new Timer(timerAutoEvent => TimerEvent(), null, 0, _settings.FixtureCheckerFrequency);

                _logger.InfoFormat("Adapter fully initiated - Started");
                _Stats.SetValue(AdapterKeys.STATUS, "Started");
            }
            catch (Exception ex)
            {
                _logger.Fatal("A fatal error has occurred and the Adapater cannot start. You can try a manual restart", ex);
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(AdapterKeys.STATUS, "Error");                
            }
        }

        public void Stop()
        {
            _Stats.SetValue(AdapterKeys.STATUS, "Closing");

            if (_trigger != null)
            {
                _trigger.Dispose();
                _trigger = null;

                if (_listeners != null)
                {
                    try
                    {
                        StopListenerAndSuspendMarkets();
                    }
                    catch (AggregateException ax)
                    {
                        foreach (var exception in ax.InnerExceptions)
                        {
                            _logger.Error(exception);
                        }
                    }
                }

                _eventState.WriteToFile();
            }

            if (_platformConnector != null)
                _platformConnector.Dispose();

            _Stats.SetValue(AdapterKeys.STOP_TIME, DateTime.Now.ToUniversalTime().ToString());
            _Stats.SetValue(AdapterKeys.STATUS, "Stopped");
        }

        private void StopListenerAndSuspendMarkets()
        {
            _logger.Debug("Stopping listeners and suspending fixtures as service is shouting down");

            Parallel.ForEach(
                _listeners.Values,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                listener =>
                {
                    listener.Stop();

                    if (_settings.SuspendAllMarketsOnShutdown)
                    {
                        listener.SuspendMarkets();
                    }
                });
        }

        /// <summary>
        /// This method could be accessed by multi threads if the ProcessSport takes long time
        /// </summary>
        private void TimerEvent()
        {
            try
            {
                _logger.DebugFormat("Timer is querying API for fixtures");

                GetStatistics();

                var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                Parallel.ForEach(
                    _sports,
                    parallelOptions, ProcessSport);

                _logger.DebugFormat("Fixture creation queueSize={0}", _resourceCreationQueue.Count);
            }
            catch (Exception ex)
            {
                if (ex is AggregateException)
                {
                    var ae = ex as AggregateException;
                    foreach (var exception in ae.InnerExceptions)
                    {
                        _logger.Error("Error processing sports: ", exception);
                        ReconnectAPI();
                    }
                }
                else
                {
                    _logger.Error("Error processing sports: ", ex);
                    ReconnectAPI();
                }
            }
            finally
            {
                lock (_sync)
                    _eventState.WriteToFile();
            }
        }

        private void ReconnectAPI()
        {
            _Stats.SetValue(AdapterKeys.STATUS, "Disconnected");
            var success = false;
            var attempts = 0;
            while (!success || attempts > _settings.MaxRetryAttempts)
            {
                try
                {
                    lock (_sync)
                    {
                        _udapiServiceFacade.Connect();
                        success = true;
                    }

                    _Stats.SetValue(AdapterKeys.STATUS, "Connected");
                }
                catch (Exception ex)
                {
                    attempts++;
                    _logger.ErrorFormat("Error trying to create a new session: {0}", ex);
                }
            }
            if (!success)
            {
                _logger.Warn("Failed to create a session!");
            }
        }

        public void AddSport(string sport)
        {
            if (!_sports.Contains(sport))
                _sports.Add(sport);
        }

        private void GetStatistics()
        {
            var queueSize = _resourceCreationQueue.Count;
            var currentlyConnected = _listeners.Count;

            try
            {
                _Stats.SetValue(AdapterKeys.QUEUE_SIZE, queueSize);
                _Stats.SetValue(AdapterKeys.TOTAL_MEMORY, GC.GetTotalMemory(false).ToString());
                _Stats.SetValue(AdapterKeys.RUNNING_THREADS, Process.GetCurrentProcess().Threads.Count);
            }
            catch { }

            _logger.InfoFormat("Currently adapter is streaming fixtureCount={0}, creation queue is queueSize={1}", currentlyConnected, queueSize);

        }

        private int OrderResultForProcessSport(IResourceFacade x, IResourceFacade y)
        {
            if (x.Content.MatchStatus > y.Content.MatchStatus)
            {
                return -1;
            }

            if (x.Content.MatchStatus < y.Content.MatchStatus)
            {
                return 1;
            }

            return DateTime.Parse(x.Content.StartTime).CompareTo(DateTime.Parse(y.Content.StartTime));
        }

        public void ProcessSport(string sport)
        {
            _logger.InfoFormat("Getting the list of available fixtures for sport={0} from GTP", sport);

            var resources = _udapiServiceFacade.GetResources(sport);

            if (resources == null)
            {
                _logger.InfoFormat("Cannot find sport={0} in UDAPI....", sport);
                return;
            }
            else if (resources.Count == 0)
            {
                _logger.InfoFormat("There are currently no fixtures for sport={0} in UDAPI", sport);
            }
            else
            {
                var processingFactor = resources.Count / 10;

                _logger.DebugFormat("Received {0} fixtures to process in sport={1}", resources.Count, sport);

                var po = new ParallelOptions() { MaxDegreeOfParallelism = processingFactor == 0 ? 1 : processingFactor };

                if (resources.Count > 1)
                {
                    resources.Sort(OrderResultForProcessSport);
                }

                Parallel.ForEach(resources,
                    po,
                    resource =>
                    {
                        try
                        {
                            ProcessResource(sport, resource);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                string.Format("Listener couldn't be created for sport={0} {1}", sport,
                                              resource.ToString()), ex);
                        }
                    });
            }

            RemoveDeletedFixtures(sport, resources);
            
            _logger.DebugFormat("Finished processing sport={0}", sport);
        }

        private void RemoveDeletedFixtures(string sport, List<IResourceFacade> resources)
        {
            var currentfixturesLookup = resources.ToDictionary(r => r.Id);
            var allFixturesForSport = _eventState.GetFixtures(sport);

            var deletedFixtures = allFixturesForSport.Where(fixtureId => !currentfixturesLookup.ContainsKey(fixtureId));
            
            foreach (var deletedFixtureId in deletedFixtures)
            {
                _logger.DebugFormat("Fixture with fixtureId={0} was deleted from Connect fixture factory",deletedFixtureId);
                RemoveAndStopListener(deletedFixtureId);
                _eventState.RemoveFixture(sport,deletedFixtureId);
            }
        }

        private void ProcessResource(string sport, IResourceFacade resource)
        {
            _logger.DebugFormat("Processing fixtureId={0} fixtureName={1} for sport={2}", resource.Id, resource.Name, sport);

            if (_listeners.ContainsKey(resource.Id))
            {
                if (_listeners[resource.Id].IsFixtureDeleted)
                {
                    _logger.DebugFormat("Fixture was deleted and republished {0}",resource);
                    RemoveAndStopListener(resource.Id);
                }
                else
                {
                    StopListenerIfFixtureEnded(sport, resource);
                    if (ValidateStream(resource))
                        return;
                }
            }

            // Check fixture is not yet over, ignore if over
            var fixtureState = _eventState.GetFixtureState(resource.Id);
            if (resource.IsMatchOver && (fixtureState == null || fixtureState.MatchStatus == resource.MatchStatus))
            {
                _logger.DebugFormat("{0} has finished. Will not process", resource.ToString());
                return;
            }

            if (!_currentlyProcessedFixtures.Contains(resource.Id))
            {
                lock (_sync)
                {
                    // another thread might be trying to process it
                    if (!_currentlyProcessedFixtures.Contains(resource.Id))
                    {
                        _logger.DebugFormat("Adding resource to queue {0}", resource);
                        _currentlyProcessedFixtures.Add(resource.Id);
                        _resourceCreationQueue.Add(resource);
                        _logger.InfoFormat("Added fixture {0} to queue", resource);
                    }
                }
            }
            else
            {
                _logger.DebugFormat("Fixture is currently being queued for creation {0}", resource);
            }
        }

        private void CreateFixture()
        {
            foreach (var resource in _resourceCreationQueue.GetConsumingEnumerable())
            {
                try
                {
                    _logger.InfoFormat("Read fixture from queue {0}", resource);
                    if (_listeners.ContainsKey(resource.Id)) continue;

                    _logger.InfoFormat("Attempting to create fixture for sport={0} and {1}", resource.Sport, resource);

                    // Get fixture snapshot and start listening (in a separate thread)
                    var fixtureSnapshot = GetSnapshot(resource);

                    if(fixtureSnapshot == null)
                        throw new ArgumentException("");

                    var lastSequenceNumber = fixtureSnapshot.Sequence;

                    _logger.DebugFormat("Processing snapshot for {0}", fixtureSnapshot);

                    var listener = _createListener(resource.Sport, resource, fixtureSnapshot, _platformConnector,
                                                   _eventState,
                                                   _marketStateObjectStore, lastSequenceNumber);

                    _Stats.AddValue(AdapterKeys.STREAMS, resource.Id);
                    listener.Start();

                    _listeners.TryAdd(resource.Id, listener);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("There has been a problem creating fixture {0}", resource), ex);
                }
                finally
                {
                    lock (_sync)
                        _currentlyProcessedFixtures.Remove(resource.Id);

                    _logger.InfoFormat("Finished processing fixture from queue {0}", resource);
                }
            }
        }

        private bool RemoveAndStopListener(string fixtureId)
        {
            _logger.DebugFormat("Removing listener for fixtureId={0}",fixtureId);
            IListener listener = null;
            _listeners.TryRemove(fixtureId, out listener);

            if (listener != null)
            {
                _Stats.RemoveValue(AdapterKeys.STREAMS, fixtureId);
                listener.Stop();
            }

            return listener != null;
        }

        private bool ValidateStream(IResourceFacade resource)
        {
            // already removed
            if (!_listeners.ContainsKey(resource.Id)) return true;

            var listener = _listeners[resource.Id];
            var maxPeriodWithoutMessage = _settings.EchoInterval * 3;

            // first check for IsErrored as it may have already suspended all markets
            if (listener.IsErrored || !listener.CheckStreamHealth(maxPeriodWithoutMessage, resource.Content != null ? resource.Content.Sequence : -1))
            {
                RemoveAndStopListener(resource.Id);
                _logger.WarnFormat("Removed resource for {0}, it will be recreated from scratch during next resource pull from API", resource);
                return false;                
            }

            return true;
        }

        private void StopListenerIfFixtureEnded(string sport, IResourceFacade resource)
        {
            _logger.DebugFormat("{0} is currently being processed", resource.ToString());

            var listener = _listeners[resource.Id];

            if (listener.IsFixtureEnded || resource.IsMatchOver)
            {
                FixtureState currState = _eventState.GetFixtureState(resource.Id);

                if (currState != null && currState.MatchStatus != MatchStatus.MatchOver)
                {
                    _logger.InfoFormat("skipping event state cleanup for {0}", resource);
                    return;
                }

                _logger.InfoFormat("{0} is over.", resource.ToString());
                
                if (RemoveAndStopListener(resource.Id))
                {
                    _eventState.RemoveFixture(sport, resource.Id);
                }
                else
                {
                    _logger.WarnFormat("Couldn't remove listener for matchOver fixture {0}", resource.ToString());
                }
            }

            if (listener.IsFixtureSetup && (resource.Content.MatchStatus != (int)MatchStatus.Setup && resource.Content.MatchStatus != (int)MatchStatus.Ready))
            {
                _logger.InfoFormat("{0} is no longer in Setup stage so the listener is now connecting to streaming server", resource.ToString());

                listener.StartStreaming();
            }
        }

        private Fixture GetSnapshot(IResourceFacade resource)
        {
            _logger.InfoFormat("Get UDAPI Snapshot for {0}", resource.ToString());

            var snapshot = resource.GetSnapshot();
            var fixtureSnapshot = snapshot.FromJson<Fixture>();

            if (string.IsNullOrEmpty(fixtureSnapshot.Id))
            {
                var exception = snapshot.FromJson<Exception>();
                throw exception;
            }

            _logger.InfoFormat("Successfully retrieved UDAPI Snapshot for {0}", fixtureSnapshot.ToString());

            return fixtureSnapshot;
        }

        private void LogVersions()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly.GetName().Version;
            var e = version.ToString();

            var sdkAssembly = Assembly.GetAssembly(typeof(ISession));
            var sdkVersion = sdkAssembly.GetName().Version;
            var s = sdkVersion.ToString();

            _logger.InfoFormat("Sporting Solutions Adapter version {0} using Sporting Solutions SDK version {1}", e, s);

            _Stats.SetValue(AdapterKeys.HOST_NAME, System.Environment.MachineName);
            _Stats.SetValue(AdapterKeys.START_TIME, DateTime.Now.ToUniversalTime().ToString());
        }
    }
}

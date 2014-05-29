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
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Plugin.Model.Interface;
using SS.Integration.Adapter.ProcessState;
using log4net;
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
        private readonly static object _sync = new object();
        private readonly ILog _logger = LogManager.GetLogger(typeof(Adapter).ToString());
        
        public event EventHandler StreamCreated;

        private readonly ConcurrentDictionary<string, IListener> _listeners;
        private readonly List<string> _sports;
        private readonly BlockingCollection<IResourceFacade> _resourceCreationQueue;
        private readonly HashSet<string> _currentlyProcessedFixtures;
        private readonly CancellationTokenSource _creationQueueCancellationToken;
        private readonly Task[] _creationTasks;
        private readonly IStatsHandle _Stats;
        private Timer _trigger;

        public Adapter(ISettings settings, IServiceFacade udapiServiceFacade, IAdapterPlugin platformConnector, IMappingUpdater mappingUpdater)
        {
            Settings = settings;
            UDAPIService = udapiServiceFacade;
            PlatformConnector = platformConnector;
            MappingUpdater = mappingUpdater;
            EventState = ProcessState.EventState.Create(new FileStoreProvider(), settings);
            

            StateProvider = new CachedObjectStoreWithPersistance<IUpdatableMarketStateCollection>(
                new BinaryStoreProvider<IUpdatableMarketStateCollection>(settings.MarketFiltersDirectory, "FilteredMarkets-{0}.bin"),
                "MarketFilters", settings.CacheExpiryInMins * 60
                );

            ThreadPool.SetMinThreads(500, 500);

            _resourceCreationQueue = new BlockingCollection<IResourceFacade>(new ConcurrentQueue<IResourceFacade>());
            _currentlyProcessedFixtures = new HashSet<string>();
            _listeners = new ConcurrentDictionary<string, IListener>();
            _sports = new List<string>();
            _creationQueueCancellationToken = new CancellationTokenSource();
            
            _creationTasks = new Task[settings.FixtureCreationConcurrency];

            _Stats = StatsManager.Instance["Adapter"].GetHandle();
            _Stats.SetValue(AdapterKeys.STATUS, "Created");          
        }


        internal IEventState EventState { get; set; }

        internal IObjectProvider<IUpdatableMarketStateCollection> StateProvider { get; set; }

        internal IAdapterPlugin PlatformConnector { get; private set; }

        internal ISettings Settings { get; private set; }

        internal IMappingUpdater MappingUpdater { get; private set; }

        internal IServiceFacade UDAPIService { get; private set; }

        /// <summary>
        /// Starts the adapter.
        /// This method returns immediately leaving to a background worker
        /// the task of getting the data and process it.
        /// 
        /// Throws an exception if it can't initialise itself.
        /// </summary>
        public void Start()
        {
            try
            {
                LogVersions();

                UDAPIService.Connect();

                _Stats.SetValue(AdapterKeys.STATUS, "Connected");

                _logger.Debug("Initialising adapter...");

                for (var i = 0; i < Settings.FixtureCreationConcurrency; i++)
                {
                    _creationTasks[i] = Task.Factory.StartNew(CreateFixture, _creationQueueCancellationToken.Token);
                }

                foreach (var sport in UDAPIService.GetSports())
                {
                    _sports.Add(sport.Name);
                }

                _trigger = new Timer(timerAutoEvent => TimerEvent(), null, 0, Settings.FixtureCheckerFrequency);

                _logger.InfoFormat("Adapter initialised");
                _Stats.SetValue(AdapterKeys.STATUS, "Started");
            }
            catch (Exception ex)
            {
                _logger.Fatal("A fatal error has occurred and the Adapater cannot start. You can try a manual restart", ex);
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(AdapterKeys.STATUS, "Error");                
                throw;
            }
        }

        /// <summary>
        /// Allows to stop the adapter.
        /// 
        /// Before returning, and if it is so configured,
        /// the adapter sends a "suspend" request to 
        /// all the currently registred fixtures.
        /// </summary>
        public void Stop()
        {
            _Stats.SetValue(AdapterKeys.STATUS, "Closing");

            try
            {
                if (_trigger != null)
                {
                    _trigger.Dispose();
                    _trigger = null;

                    _creationQueueCancellationToken.Cancel(false);

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

                    EventState.WriteToFile();
                    MappingUpdater.Dispose();

                    Task.WaitAll(_creationTasks);

                    _resourceCreationQueue.Dispose();
                    _creationQueueCancellationToken.Dispose();
                }

                if (PlatformConnector != null)
                    PlatformConnector.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("An error occured while disposing the adapter", e);
            }

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

                    if (Settings.SuspendAllMarketsOnShutdown)
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
                _logger.DebugFormat("Adapter is querying API for fixtures");

                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

                Parallel.ForEach(_sports, parallelOptions, ProcessSport);

                GetStatistics();

            }
            catch (Exception ex)
            {
                if (ex is AggregateException)
                {
                    var ae = ex as AggregateException;
                    foreach (var exception in ae.InnerExceptions)
                    {
                        _logger.Error("Error processing sports: ", exception);
                    }
                }
                else
                {
                    _logger.Error("Error processing sports: ", ex);
                }

                ReconnectAPI();
            }
            finally
            {
                lock (_sync)
                    EventState.WriteToFile();
            }
        }

        private void ReconnectAPI()
        {
            _Stats.SetValue(AdapterKeys.STATUS, "Disconnected");
            var success = false;
            var attempts = 0;
            while (!success || attempts > Settings.MaxRetryAttempts)
            {
                try
                {
                    lock (_sync)
                    {
                        UDAPIService.Connect();
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

        internal void AddSport(string sport)
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
                _Stats.SetValueUnsafe(AdapterKeys.QUEUE_SIZE, queueSize);
                _Stats.SetValueUnsafe(AdapterKeys.TOTAL_MEMORY, GC.GetTotalMemory(false).ToString());
                _Stats.SetValueUnsafe(AdapterKeys.RUNNING_THREADS, Process.GetCurrentProcess().Threads.Count);
            }
            catch { }

            _logger.DebugFormat("Currently adapter is streaming fixtureCount={0}, creation queue is queueSize={1}", currentlyConnected, queueSize);

        }

        internal void ProcessSport(string sport)
        {
            _logger.DebugFormat("Getting the list of available fixtures for sport={0} from GTP", sport);

            var resources = UDAPIService.GetResources(sport);

            if (resources == null)
            {
                _logger.WarnFormat("Cannot find sport={0} in UDAPI....", sport);
                return;
            }
            
            if (resources.Count == 0)
            {
                _logger.DebugFormat("There are currently no fixtures for sport={0} in UDAPI", sport);
                return;
            }
            
            var processingFactor = resources.Count / 10;

            _logger.DebugFormat("Received count={0} fixtures to process in sport={1}", resources.Count, sport);

            var po = new ParallelOptions { MaxDegreeOfParallelism = processingFactor == 0 ? 1 : processingFactor };

            if (resources.Count > 1)
            {
                resources.Sort((x, y) =>
                {
                    if (x.Content.MatchStatus > y.Content.MatchStatus)
                        return -1;

                    return x.Content.MatchStatus < y.Content.MatchStatus ? 1 : DateTime.Parse(x.Content.StartTime).CompareTo(DateTime.Parse(y.Content.StartTime));
                });
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
                        _logger.Error(string.Format("Listener couldn't be created for sport={0} and resource={1}", sport, resource.ToString()), ex);
                    }
                });
            

            RemoveDeletedFixtures(sport, resources);
            
            _logger.DebugFormat("Finished processing sport={0}", sport);
        }

        private void RemoveDeletedFixtures(string sport, IEnumerable<IResourceFacade> resources)
        {
            var currentfixturesLookup = resources.ToDictionary(r => r.Id);
            var allFixturesForSport = EventState.GetFixtures(sport);

            var deletedFixtures = allFixturesForSport.Where(fixtureId => !currentfixturesLookup.ContainsKey(fixtureId));
            
            foreach (var deletedFixtureId in deletedFixtures)
            {
                _logger.DebugFormat("Fixture with fixtureId={0} was deleted from Connect fixture factory", deletedFixtureId);
                RemoveAndStopListener(deletedFixtureId);
                EventState.RemoveFixture(sport,deletedFixtureId);
            }
        }

        private void ProcessResource(string sport, IResourceFacade resource)
        {
            _logger.InfoFormat("Processing {0} for sport={1}", resource, sport);

            // this prevents to take any decision
            // about the resource while it is
            // being processed by another thread
            lock (_sync)
            {
                if (_currentlyProcessedFixtures.Contains(resource.Id))
                {
                    _logger.DebugFormat("{0} is currently being processed - skipping it", resource);
                    return;
                }

                _currentlyProcessedFixtures.Add(resource.Id);
            }

            if (_listeners.ContainsKey(resource.Id))
            {

                IListener listener = _listeners[resource.Id];

                if (listener.IsFixtureDeleted)
                {
                    _logger.DebugFormat("{0} was deleted and republished", resource);
                    RemoveAndStopListener(resource.Id);
                }
                else if (listener.IsIgnored)
                {
                    _logger.DebugFormat("{0} is marked as ignored", resource);
                    RemoveAndStopListener(resource.Id);
                }
                else
                {
                    if (StopListenerIfFixtureEnded(sport, resource))
                        return;

                    // if the stream is valid, update its status
                    if (ValidateStream(resource))
                    {
                        _listeners[resource.Id].UpdateResourceState(resource);
                        return;
                    }

                    _logger.WarnFormat("Removed resource for {0}, it will be recreated from scratch during next resource pull from API", resource);
                    RemoveAndStopListener(resource.Id);
                }
            }

            // Check fixture is not yet over, ignore if over
            var fixtureState = EventState.GetFixtureState(resource.Id);
            if (resource.IsMatchOver && (fixtureState == null || fixtureState.MatchStatus == resource.MatchStatus))
            {
                _logger.InfoFormat("{0} has finished. Will not process", resource);
                return;
            }


            _logger.DebugFormat("Adding {0} to the queue ", resource);
            _resourceCreationQueue.Add(resource);
            _logger.InfoFormat("Added {0} to the queue", resource);

        }

        private void CreateFixture()
        {
            try
            {
                foreach (var resource in _resourceCreationQueue.GetConsumingEnumerable(_creationQueueCancellationToken.Token))
                {
                    try
                    {
                        _logger.DebugFormat("Read {0} from the queue", resource);

                        if (_listeners.ContainsKey(resource.Id))
                            continue;

                        _logger.InfoFormat("Attempting to create fixture for sport={0} and {1}", resource.Sport, resource);

                        var listener = new StreamListener(resource, PlatformConnector, EventState, StateProvider);

                        _Stats.AddValue(AdapterKeys.STREAMS, resource.Id);
                        listener.Start();
                        _listeners.TryAdd(resource.Id, listener);

                        OnStreamCreated();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(string.Format("There has been a problem creating fixture {0}", resource), ex);
                    }
                    finally
                    {
                        lock (_sync)
                        {
                            _currentlyProcessedFixtures.Remove(resource.Id);
                        }

                        _logger.InfoFormat("Finished processing fixture from queue {0}", resource);
                    }

                    if (_creationQueueCancellationToken.IsCancellationRequested)
                    {
                        _logger.DebugFormat("Fixture creation task={0} will terminate as requested", Task.CurrentId);
                        break;
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                _logger.DebugFormat("Fixture creation task={0} exited as requested", Task.CurrentId);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("An error occured on fixture creation task={0}", Task.CurrentId), ex);
            }

        }

        private bool RemoveAndStopListener(string fixtureId)
        {
            _logger.DebugFormat("Removing listener for fixtureId={0}", fixtureId);
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
            if (!_listeners.ContainsKey(resource.Id)) 
                return true;

            var listener = _listeners[resource.Id];
            var maxPeriodWithoutMessage = Settings.EchoInterval * 3;

            return listener.CheckStreamHealth(maxPeriodWithoutMessage, resource.Content.Sequence);
        }

        /// <summary>
        /// Stops and remove the listener if the fixture is over.
        /// Returns true if the fixture was over and the listener is removed.
        /// False otherwise
        /// </summary>
        /// <param name="sport"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        private bool StopListenerIfFixtureEnded(string sport, IResourceFacade resource)
        {
            _logger.DebugFormat("{0} is currently being processed", resource);

            var listener = _listeners[resource.Id];

            if (listener.IsFixtureEnded || resource.IsMatchOver)
            {
                FixtureState currState = EventState.GetFixtureState(resource.Id);

                if (currState != null && currState.MatchStatus != MatchStatus.MatchOver)
                {
                    _logger.InfoFormat("Skipping event state cleanup for {0}", resource);
                    return false;
                }

                _logger.InfoFormat("{0} is over.", resource);
                
                if (RemoveAndStopListener(resource.Id))
                {
                    EventState.RemoveFixture(sport, resource.Id);
                }
                else
                {
                    _logger.WarnFormat("Couldn't remove listener for matchOver fixture {0}", resource);
                }

                return true;
            }

            return false;
        }

        private void LogVersions()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly.GetName().Version;
            var e = version.ToString();

            var sdkAssembly = Assembly.GetAssembly(typeof(ISession));
            var sdkVersion = sdkAssembly.GetName().Version;
            var s = sdkVersion.ToString();

            _logger.InfoFormat("Sporting Solutions Adapter version={0} using Sporting Solutions SDK version={1}", e, s);

            _Stats.SetValue(AdapterKeys.HOST_NAME, Environment.MachineName);
            _Stats.SetValue(AdapterKeys.START_TIME, DateTime.Now.ToUniversalTime().ToString());
        }

        protected virtual void OnStreamCreated()
        {
            if (StreamCreated != null)
            {
                StreamCreated(this, EventArgs.Empty);
            }
        }
    }
}

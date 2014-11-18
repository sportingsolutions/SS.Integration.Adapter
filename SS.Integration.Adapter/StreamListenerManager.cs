using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SportingSolutions.Udapi.Sdk;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.ProcessState;

namespace SS.Integration.Adapter
{
    public class StreamListenerManager : IStreamListenerManager
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerManager));
        private const int LISTENER_DISPOSING_SAFE_GUARD = 1;

        private readonly ConcurrentDictionary<string, IListener> _listeners = new ConcurrentDictionary<string, IListener>();
        private readonly ConcurrentDictionary<string, int> _listenerDisposingQueue = new ConcurrentDictionary<string, int>();
        private readonly HashSet<string> _currentlyProcessedFixtures = new HashSet<string>();
        private readonly static object _sync = new object();

        public event Adapter.StreamEventHandler StreamCreated;
        public event Adapter.StreamEventHandler StreamRemoved;

        internal IEventState EventState { get; set; }

        public StreamListenerManager(ISettings settings)
        {
            EventState = ProcessState.EventState.Create(new FileStoreProvider(), settings);
        }

        protected IListener GetStreamListener(string fixtureId)
        {
            if (!HasStreamListener(fixtureId)) return null;
            return _listeners[fixtureId];
        }

       
        public void UpdateCurrentlyAvailableFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup)
        {
            var allFixturesForSport = _listeners.Where(x => string.Equals(x.Value.Sport, sport, StringComparison.Ordinal));

            // deletedFixtures = allFixturesForSport \ resources
            var deletedFixtures = allFixturesForSport.Where(fixture => !currentfixturesLookup.ContainsKey(fixture.Key));

            // existingFixtures = resources ^ allFixturesForSport
            var existingFixtures = allFixturesForSport.Where(fixture => currentfixturesLookup.ContainsKey(fixture.Key));


            foreach (var fixture in deletedFixtures)
            {
                if (_listenerDisposingQueue.ContainsKey(fixture.Key))
                {
                    if (_listenerDisposingQueue[fixture.Key] >= LISTENER_DISPOSING_SAFE_GUARD)
                    {

                        _logger.DebugFormat("Fixture with fixtureId={0} was deleted from Connect fixture factory", fixture.Key);
                        RemoveStreamListener(fixture.Key);
                        EventState.RemoveFixture(fixture.Key);
                    }
                    else
                    {
                        _listenerDisposingQueue[fixture.Key] = _listenerDisposingQueue[fixture.Key] + 1;
                    }
                }
                else
                {
                    _listenerDisposingQueue.TryAdd(fixture.Key, 1);
                    _logger.DebugFormat("Fixture with fixtureId={0} has been added to the disposing queue", fixture.Key);
                }
            }

            foreach (var fixture in existingFixtures)
            {
                if (_listenerDisposingQueue.ContainsKey(fixture.Key))
                {
                    int dummy;
                    _listenerDisposingQueue.TryRemove(fixture.Key, out dummy);
                    _logger.DebugFormat("Fixture with fixtureId={0} was marked as deleted, but it appered on Connect again", fixture.Key);
                }
            }

            SaveEventState();
        }

        public bool RemoveStreamListener(string fixtureId)
        {
            _logger.InfoFormat("Removing listener for fixtureId={0}", fixtureId);

            IListener listener = null;
            _listeners.TryRemove(fixtureId, out listener);

            if (listener != null)
            {
                listener.Dispose();
                OnStreamRemoved(fixtureId);
            }

            return listener != null;
        }

        public IEnumerable<IGrouping<string, IListener>> GetListenersBySport()
        {
            return _listeners.Values.GroupBy(x => x.Sport);
        }

        public bool WillProcessResource(IResourceFacade resource)
        {
            if (HasStreamListener(resource.Id))
            {
                _logger.DebugFormat("Listener already exists for {0}", resource);

                IListener listener = _listeners[resource.Id];

                if (listener.IsFixtureDeleted)
                {
                    _logger.DebugFormat("{0} was deleted and republished. Listener wil be removed", resource);
                    RemoveStreamListener(resource.Id);
                }
                else if (listener.IsIgnored)
                {
                    _logger.DebugFormat("{0} is marked as ignored. Listener wil be removed", resource);
                    RemoveStreamListener(resource.Id);
                }
                else
                {
                    if (!RemoveStreamListenerIfFinishedProcessing(resource))
                    {
                        _listeners[resource.Id].UpdateResourceState(resource);
                    }
                }

                MarkResourceAsProcessable(resource);
                return false;
            }
            else
            {
                // Check fixture is not yet over, ignore if over
                var fixtureState = EventState.GetFixtureState(resource.Id);
                if (resource.IsMatchOver && (fixtureState == null || fixtureState.MatchStatus == resource.MatchStatus))
                {
                    _logger.InfoFormat("{0} is over. Adapter will not process the resource", resource);
                    MarkResourceAsProcessable(resource);
                    return false;
                }

                //the resource will be processed
                return true;
            }
        }

        public virtual void CreateStreamListener(IResourceFacade resource, IStateManager stateManager, IAdapterPlugin platformConnector)
        {
            try
            {
                _logger.DebugFormat("Attempting to create a Listener for sport={0} and {1}", resource.Sport, resource);
                
                var listener = new StreamListener(resource, platformConnector, EventState, stateManager);

                if (!listener.Start())
                {
                    _logger.WarnFormat("Couldn't start stream listener for {0}", resource);
                    return;
                }

                _listeners.TryAdd(resource.Id, listener);

                OnStreamCreated(resource.Id);

                _logger.InfoFormat("Listener created for {0}", resource);
            }
            finally
            {
                MarkResourceAsProcessable(resource);

                SaveEventState();
                _logger.DebugFormat("Finished processing fixture {0}", resource);
            }
        }



        public bool RemoveStreamListenerIfFinishedProcessing(IResourceFacade resource)
        {
            var listener = _listeners[resource.Id];

            if (listener.IsFixtureEnded || resource.IsMatchOver)
            {
                _logger.DebugFormat("{0} is marked as ended - checking for stopping streaming", resource);

                var currentState = EventState.GetFixtureState(resource.Id);

                if (currentState != null && currentState.MatchStatus != MatchStatus.MatchOver)
                {
                    _logger.DebugFormat("{0} is over but the MatchOver update has not been processed yet", resource);
                    return false;
                }

                _logger.InfoFormat("{0} is over. Listener will be removed", resource);

                if (RemoveStreamListener(resource.Id))
                {
                    EventState.RemoveFixture(resource.Id);
                }
                else
                {
                    _logger.WarnFormat("Couldn't remove listener for matchOver fixture {0}", resource);
                }

                return true;
            }

            return false;
        }

        public bool CanBeProcessed(string fixtureId)
        {
            // this prevents to take any decision
            // about the resource while it is
            // being processed by another thread
            lock (_sync)
            {
                if (_currentlyProcessedFixtures.Contains(fixtureId))
                {
                    _logger.DebugFormat("Fixture fixtureId={0} is currently being processed by another task - ignoring it", fixtureId);
                    return false;
                }

                _currentlyProcessedFixtures.Add(fixtureId);
            }

            return true;
        }

        public void RemoveFixtureEventState(string fixtureId)
        {
            EventState.RemoveFixture(fixtureId);
        }

        public bool HasStreamListener(string fixtureId)
        {
            return _listeners.ContainsKey(fixtureId);
        }

        public virtual void StartStreaming(string fixtureId)
        {
            if (!HasStreamListener(fixtureId)) return;

            _listeners[fixtureId].Start();
        }

        public virtual void StopStreaming(string fixtureId)
        {
            if (!HasStreamListener(fixtureId)) return;

            _listeners[fixtureId].Stop();
        }
        
        public int ListenersCount { get { return _listeners.Count; } }

        public void StopAll()
        {
            if (_listeners != null)
            {
                try
                {
                    DisposeListeners();
                }
                catch (AggregateException ax)
                {
                    foreach (var exception in ax.InnerExceptions)
                    {
                        _logger.Error("Error during listener disposing", exception);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error("Error during listener disposing", e);
                }

                _listeners.Clear();
            }

            SaveEventState();
        }

        private void DisposeListeners()
        {
            _logger.Debug("Stopping all listeners and suspending all fixtures as requested");

            Parallel.ForEach(
                _listeners.Values,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                listener => listener.Dispose());
        }

        protected virtual void OnStreamCreated(string fixtureId)
        {
            if (StreamCreated != null)
            {
                StreamCreated(this, fixtureId);
            }
        }

        protected virtual void OnStreamRemoved(string fixtureId)
        {
            if (StreamRemoved != null)
                StreamRemoved(this, fixtureId);
        }

        private void MarkResourceAsProcessable(IResourceFacade resource)
        {
            lock (_sync)
            {
                if (_currentlyProcessedFixtures.Contains(resource.Id))
                    _currentlyProcessedFixtures.Remove(resource.Id);
            }
        }

        private void SaveEventState()
        {
            try
            {
                lock (_sync)
                    EventState.WriteToFile();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Event state errored on attempt to save it: {0}",ex);
            }
        }
    }
}

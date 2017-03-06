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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SportingSolutions.Udapi.Sdk;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter
{
    public class StreamListenerManager : IStreamListenerManager
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerManager));
        private const int LISTENER_DISPOSING_SAFE_GUARD = 1;

        protected readonly ConcurrentDictionary<string, IListener> _listeners = new ConcurrentDictionary<string, IListener>();
        protected readonly ConcurrentDictionary<string, bool> _createListener = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, int> _listenerDisposingQueue = new ConcurrentDictionary<string, int>();
        protected readonly ConcurrentDictionary<string, int> _currentlyProcessedFixtures = new ConcurrentDictionary<string, int>();
        private readonly static object _sync = new object();
        protected readonly ISettings _settings;

        public event Adapter.StreamEventHandler StreamCreated;
        public event Adapter.StreamEventHandler StreamRemoved;

        public IEventState EventState { get; set; }

        public StreamListenerManager(ISettings settings)
        {
            _settings = settings;
            EventState = Model.EventState.Create(new FileStoreProvider(settings.StateProviderPath), settings);
        }

        public IStateManager StateManager { get; set; }

        public Action<string> ProcessResourceHook { get; set; }

        protected IListener GetStreamListener(string fixtureId)
        {
            if (!HasStreamListener(fixtureId)) return null;
            return _listeners[fixtureId];
        }

       
        public virtual void UpdateCurrentlyAvailableFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup)
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

                        _logger.InfoFormat("Fixture with fixtureId={0} was deleted from Connect fixture factory", fixture.Key);
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
                    _logger.InfoFormat("Fixture with fixtureId={0} has been added to the disposing queue", fixture.Key);
                }
            }

            foreach (var fixture in existingFixtures)
            {
                if (_listenerDisposingQueue.ContainsKey(fixture.Key))
                {
                    int dummy;
                    _listenerDisposingQueue.TryRemove(fixture.Key, out dummy);
                    _logger.InfoFormat("Fixture with fixtureId={0} was marked as deleted, but it appered on Connect again", fixture.Key);
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

        public bool ShouldProcessResource(IResourceFacade resource)
        {
            if (HasStreamListener(resource.Id))
            {
                _logger.DebugFormat("Listener already exists for {0}", resource);

                IListener listener = _listeners[resource.Id];

                var shouldAdapterProcessResource = false;

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
                //Disconnected from the stream - this fixture should be reconnected ASAP
                else if (listener.IsDisconnected && (resource.MatchStatus == MatchStatus.Prematch || resource.MatchStatus == MatchStatus.InRunning))
                {
                    _logger.WarnFormat("{0} was disconnected from stream {1}", resource, resource.MatchStatus);
                    RemoveStreamListener(resource.Id);
                    
                    shouldAdapterProcessResource = true;
                }
                else
                {
                    if (!RemoveStreamListenerIfFinishedProcessing(resource))
                    {
                        _listeners[resource.Id].UpdateResourceState(resource);
                    }
                }
                return shouldAdapterProcessResource;
            }
            else
            {
                // Check fixture is not yet over, ignore if over
                var fixtureState = EventState.GetFixtureState(resource.Id);
                if (resource.IsMatchOver && (fixtureState == null || fixtureState.MatchStatus == resource.MatchStatus))
                {
                    _logger.InfoFormat("{0} is over. Adapter will not process the resource", resource);
                    return false;
                }

                if (_createListener.ContainsKey(resource.Id))
                {
                    _logger.DebugFormat("Listener for {0} is not created yet. It is creating right now.", resource);
                    return false;
                }

                _logger.DebugFormat("Listener for {0} is not created yet. Adapter will add resource to the creation queue", resource);
                //the resource will added to the queue
                return true;
            }
        }

        public virtual void CreateStreamListener(IResourceFacade resource, IAdapterPlugin platformConnector)
        {
            bool creationWasLocked = false;
            try
            {
                _logger.InfoFormat("Attempting to create a Listener for sport={0} and {1}", resource.Sport, resource);

                if (_listeners.ContainsKey(resource.Id))
                {
                    _logger.InfoFormat("Stream listener already exists for {0}, skipping creation",resource);
                    return;
                }

                // this is king of lock that prevent to create 2 listener for  the same resource.Id
                if (LockCreatingListener(resource))
                    return;
                creationWasLocked = true;

                var listener = CreateStreamListenerObject(resource, platformConnector, EventState, StateManager);

                var isStarted = listener.Start();

                if (!isStarted)
                {
                    _logger.WarnFormat("Couldn't start stream listener for {0}", resource);
                    listener.Dispose();
                    DisposedStreamListener(listener);
                    return;
                }

                var added = _listeners.TryAdd(resource.Id, listener);
                if (!added)
                {
                    _logger.WarnFormat("Failed to add stream listener - most likely it has been already added {0} - this will be disposed now",resource);
                    listener.Dispose();
                    DisposedStreamListener(listener);
                    return;
                }

                OnStreamCreated(resource.Id);

                (listener as StreamListener).OnDisconnected += OnStreamDisconnected;

                _logger.InfoFormat("Listener created for {0}", resource);
            }
            finally
            {
                if (creationWasLocked)
                    ReleaseCreatingListener(resource);
                ReleaseProcessing(resource.Id);
                _logger.DebugFormat("Finished processing fixture {0}", resource);
                _logger.DebugFormat("Saving event state after processing fixture {0}", resource);
                SaveEventState();
                
            }
        }

        private void ReleaseCreatingListener(IResourceFacade resource)
        {
            bool v;
            _createListener.TryRemove(resource.Id, out v);
        }

        private bool LockCreatingListener(IResourceFacade resource)
        {
            var process = _createListener.TryAdd(resource.Id, true);
            if (!process)
            {
                _logger.InfoFormat("Another creation of listener processing right now for {0}, skipping creation", resource);
                return true;
            }
            return false;
        }

        private void OnStreamDisconnected(object sender, StreamListenerEventArgs e)
        {
            var listener = e.Listener;
            var shouldFastTrackReconnect = !listener.IsFixtureDeleted && listener.IsDisconnected && e.MatchStatus == MatchStatus.InRunning;

            if (shouldFastTrackReconnect && ProcessResourceHook != null)
            {
                _logger.InfoFormat("Fixture will be reset to creation queue fixtureId={0}",listener.FixtureId);
                ProcessResourceHook(listener.FixtureId);
            }
        }

        protected virtual void DisposedStreamListener(IListener listener)
        {
            (listener as StreamListener).OnDisconnected -= OnStreamDisconnected;
        }

        protected virtual IListener CreateStreamListenerObject(IResourceFacade resource, IAdapterPlugin platformConnector, IEventState eventState, IStateManager stateManager)
        {
            return new StreamListener(resource, platformConnector, eventState, stateManager,_settings);
        }


        private bool RemoveStreamListenerIfFinishedProcessing(IResourceFacade resource)
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

        public bool TryLockProcessing(string fixtureId)
        {
            // this prevents to take any decision
            // about the resource while it is
            // being processed by another thread
            var isFree = _currentlyProcessedFixtures.TryAdd(fixtureId, 0);
            if (!isFree)
            {
                int c = 0;
                var isReceived = _currentlyProcessedFixtures.TryGetValue(fixtureId, out c);
                if (isReceived)
                {
                    c++;
                    _currentlyProcessedFixtures.TryUpdate(fixtureId, c, c - 1);
                }
                _logger.DebugFormat("Fixture fixtureId={0} is currently being processed by another task - ignoring it. This is {1} attemp to process", fixtureId, c);
                if (c > 10)
                {
                    _logger.Warn($"Fixture fixtureId={fixtureId} failed to process {c} times, possible stacked resource");
                }

                if (c > 25)
                {
                    _logger.Warn($"Fixture fixtureId={fixtureId} failed to process {c} times, attemp to release resource");
                    ReleaseProcessing(fixtureId);
                }
            }
            return isFree;
        }

        public void ReleaseProcessing(string fixtureId)
        {
            int v;
            var removed = _currentlyProcessedFixtures.TryRemove(fixtureId, out v);
            if (!removed)
            {
                _logger.Warn($"Fixture fixtureId={fixtureId} failed to ReleaseProcessing, possible stacked resource");
            }
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
                finally
                {
                    _listeners.Clear();
                }
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


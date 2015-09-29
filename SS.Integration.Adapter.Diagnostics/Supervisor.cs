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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using log4net;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.RestService;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.Diagnostics
{
    public class Supervisor : StreamListenerManager, ISupervisor
    {
        private ILog _logger = LogManager.GetLogger(typeof(Supervisor));

        private ConcurrentDictionary<string, FixtureOverview> _fixtures;
        private readonly ConcurrentDictionary<string, SportOverview> _sportOverviews;

        private readonly Subject<IFixtureOverviewDelta> _fixtureTracker = new Subject<IFixtureOverviewDelta>();
        private readonly Subject<ISportOverview> _sportTracker = new Subject<ISportOverview>();

        private IDisposable _publisher;
        private IObjectProvider<Dictionary<string, FixtureOverview>> _objectStore;

        public Supervisor(ISettings settings, IObjectProvider<Dictionary<string, FixtureOverview>> supervisorStore)
            : base(settings)
        {
            _objectStore = supervisorStore;
            LoadState();

            _sportOverviews = new ConcurrentDictionary<string, SportOverview>();
            SetupSports();
            
        }

        private void LoadState()
        {
            Dictionary<string, FixtureOverview> storedObject = null;

            try
            {
                storedObject = _objectStore.GetObject(null);
            }
            catch (Exception ex)
            {
                _logger.Error("Error while loading Supervisor state: {0}",ex);
            }
            finally
            {
                _fixtures = storedObject != null
                    ? new ConcurrentDictionary<string, FixtureOverview>(storedObject)
                    : new ConcurrentDictionary<string, FixtureOverview>();
            }
        }

        public ISupervisorProxy Proxy { get; set; }
        public ISupervisorService Service { get; set; }

        private void SetupSports()
        {
            foreach (var sportGroup in _fixtures.Values.GroupBy(f => f.Sport))
            {
                UpdateSportDetails(sportGroup.Key);
            }

        }

        public void Initialise()
        {
            Proxy = new SupervisorProxy(this);
            Service = new Service(Proxy);
            Service.Start();
        }

        private void SaveState()
        {
            try
            {
                lock (this)
                {
                    var tempFixtures = _fixtures.Values;
                    
                    if (tempFixtures == null)
                        return;

                    //have to remove exception because it might not be serializable
                    tempFixtures.Where(x => x.LastError != null).ForEach(x => x.LastError.Exception = null);
                    _objectStore.SetObject(null, tempFixtures.ToDictionary(f => f.Id));
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Error during saving supervisor state {0}",ex);
            }
        }

        protected override IListener CreateStreamListenerObject(IResourceFacade resource, IAdapterPlugin platformConnector, IEventState eventState,
            IStateManager stateManager)
        {
            var streamListener = base.CreateStreamListenerObject(resource, platformConnector, eventState, stateManager);

            var streamListenerObject = streamListener as StreamListener;
            if (streamListenerObject != null)
            {
                streamListenerObject.OnConnected += StreamListenerConnected;
                streamListenerObject.OnDisconnected += StreamListenerDisconnected;
                streamListenerObject.OnError += StreamListenerErrored;
                streamListenerObject.OnFlagsChanged += StreamListenerFlagsChanged;
                streamListenerObject.OnBeginSnapshotProcessing += StreamListenerSnapshot;
                streamListenerObject.OnFinishedSnapshotProcessing += StreamListenerFinishedProcessingUpdate;
                streamListenerObject.OnBeginStreamUpdateProcessing += StreamListenerBeginStreamUpdate;
                streamListenerObject.OnFinishedStreamUpdateProcessing += StreamListenerFinishedProcessingUpdate;
                streamListenerObject.OnSuspend += StreamListenerSuspended;
                streamListenerObject.OnStop += StreamListenerStop;
            }
            
            return streamListener;
        }

        private void PublishDelta(FixtureOverview fixtureOverview)
        {
            var delta = fixtureOverview.GetDelta();
            if (delta != null)
                _fixtureTracker.OnNext(delta);
        }

        protected override void DisposedStreamListener(IListener listener)
        {
            var streamListener = listener as StreamListener;
            if (streamListener == null) return;

            _logger.DebugFormat("Removing stream listener for {0}", listener.FixtureId);

            streamListener.OnConnected -= StreamListenerConnected;
            streamListener.OnError -= StreamListenerErrored;
            streamListener.OnFlagsChanged -= StreamListenerFlagsChanged;
            streamListener.OnBeginSnapshotProcessing -= StreamListenerSnapshot;
            streamListener.OnBeginStreamUpdateProcessing -= StreamListenerBeginStreamUpdate;
            streamListener.OnFinishedStreamUpdateProcessing -= StreamListenerFinishedProcessingUpdate;
            streamListener.OnFinishedSnapshotProcessing -= StreamListenerFinishedProcessingUpdate;
            streamListener.OnSuspend -= StreamListenerSuspended;
            streamListener.OnStop -= StreamListenerStop;
            streamListener.OnDisconnected -= StreamListenerDisconnected;

            SaveState();
        }
        
        public override void CreateStreamListener(IResourceFacade resource, IAdapterPlugin platformConnector)
        {
            base.CreateStreamListener(resource, platformConnector);
            var listener = GetStreamListenerObject(resource.Id);

            UpdateStateFromStreamListener(listener);
            var fixtureOverview = GetFixtureOverview(listener.FixtureId) as FixtureOverview;

            PublishDelta(fixtureOverview);

            _logger.DebugFormat("Created new StreamListener for {0}", resource);
        }

        public override void StopStreaming(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId);
            base.StopStreaming(fixtureId);

            DisposedStreamListener(listener);
        }

        private void StreamListenerFinishedProcessingUpdate(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;

            if (fixtureOverview.FeedUpdate != null && fixtureOverview.FeedUpdate.Sequence == e.CurrentSequence)
            {
                var feedUpdate = fixtureOverview.FeedUpdate.Clone() as FeedUpdateOverview;
                feedUpdate.IsProcessed = true;
                feedUpdate.ProcessingTime = DateTime.UtcNow - feedUpdate.Issued;
                fixtureOverview.FeedUpdate = feedUpdate;
            }

            UpdateLastErrorResolved(e, fixtureOverview);
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerBeginStreamUpdate(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;

            fixtureOverview.FeedUpdate = CreateFeedUpdate(e);

            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerStop(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerSuspended(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;
            fixtureOverview.ListenerOverview.IsSuspended = true;
            UpdateStateFromEventDetails(e);
        }

        private FeedUpdateOverview CreateFeedUpdate(StreamListenerEventArgs streamListenerArgs, bool isSnapshot = false)
        {
            var feedUpdate = new FeedUpdateOverview
            {
                Issued = DateTime.UtcNow,
                Sequence = streamListenerArgs.CurrentSequence,
                IsSnapshot = isSnapshot,
                Epoch = streamListenerArgs.Epoch,
                LastEpochChangeReason = streamListenerArgs.LastEpochChangeReason
            };

            return feedUpdate;
        }

        private void StreamListenerSnapshot(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;

            //assumption
            fixtureOverview.ListenerOverview.IsSuspended = false;

            fixtureOverview.FeedUpdate = CreateFeedUpdate(e, true);

            UpdateStateFromEventDetails(e);

            SaveState();
        }

        private void StreamListenerFlagsChanged(object sender, StreamListenerEventArgs streamListenerEventArgs)
        {
            var fixtureOverview = GetFixtureOverview(streamListenerEventArgs.Listener.FixtureId) as FixtureOverview;

            UpdateLastErrorResolved(streamListenerEventArgs, fixtureOverview);

            UpdateStateFromEventDetails(streamListenerEventArgs);
        }

        private static void UpdateLastErrorResolved(StreamListenerEventArgs streamListenerEventArgs,
            FixtureOverview fixtureOverview)
        {
            if (fixtureOverview.LastError != null
                && fixtureOverview.LastError.IsErrored
                && !streamListenerEventArgs.Listener.IsErrored)
            {
                fixtureOverview.LastError.ResolvedAt = DateTime.UtcNow;
                fixtureOverview.LastError.IsErrored = false;
            }
        }

        private void StreamListenerErrored(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;
            fixtureOverview.LastError = new ErrorOverview
            {
                ErroredAt = DateTime.UtcNow,
                Exception = e.Exception,
                IsErrored = e.Exception != null,
                Sequence = e.CurrentSequence
            };

            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerDisconnected(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerConnected(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        public void ForceSnapshot(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId) as StreamListener;
            if (listener == null)
                throw new NullReferenceException("Can't convert Listener to StreamListener in ForceSnapshot method");

            //skips market rules
            listener.ForceSnapshot();

            _logger.InfoFormat("Forced snapshot for fixtureId={0}", fixtureId);
        }

        public override void StartStreaming(string fixtureId)
        {
            base.StartStreaming(fixtureId);
            UpdateStateFromStreamListener(fixtureId);

        }

        private void UpdateStateFromStreamListener(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId);
            UpdateStateFromStreamListener(listener as StreamListener);
        }

        private void UpdateStateFromEventDetails(StreamListenerEventArgs streamListenerEventArgs)
        {
            UpdateStateFromStreamListener(streamListenerEventArgs.Listener as StreamListener);

            var fixtureOverview = GetFixtureOverview(streamListenerEventArgs.Listener.FixtureId) as FixtureOverview;
            fixtureOverview.ListenerOverview.Sequence = streamListenerEventArgs.CurrentSequence;
            fixtureOverview.ListenerOverview.Epoch = streamListenerEventArgs.Epoch;
            fixtureOverview.ListenerOverview.StartTime = streamListenerEventArgs.StartTime ?? fixtureOverview.ListenerOverview.StartTime;
            fixtureOverview.ListenerOverview.LastEpochChangeReason = streamListenerEventArgs.LastEpochChangeReason ?? fixtureOverview.ListenerOverview.LastEpochChangeReason;

            fixtureOverview.Name = streamListenerEventArgs.Name ?? fixtureOverview.Name;
            fixtureOverview.ListenerOverview.MatchStatus = streamListenerEventArgs.MatchStatus ?? fixtureOverview.ListenerOverview.MatchStatus;

            if (streamListenerEventArgs.IsSnapshot)
            {
                fixtureOverview.CompetitionId = streamListenerEventArgs.CompetitionId;
                fixtureOverview.CompetitionName = streamListenerEventArgs.CompetitionName;
            }
            
            PublishDelta(fixtureOverview);

            UpdateSportDetails(streamListenerEventArgs.Listener.Sport);
        }

        private void UpdateSportDetails(string sportName)
        {
            var sportOverview = new SportOverview();
            sportOverview.Name = sportName;


            var fixturesForSport = _fixtures.Values.Where(f =>
                                                            f.Sport == sportOverview.Name
                                                            && (f.ListenerOverview.IsDeleted.HasValue && !f.ListenerOverview.IsDeleted.Value || !f.ListenerOverview.IsDeleted.HasValue))
                                                            .ToList();
            sportOverview.Total = fixturesForSport.Count;
            sportOverview.InErrorState = fixturesForSport.Count(f => f.ListenerOverview.IsErrored.HasValue && f.ListenerOverview.IsErrored.Value);

            var groupedByMatchStatus = fixturesForSport
                .GroupBy(f => f.ListenerOverview.MatchStatus, f => f.ListenerOverview.MatchStatus)
                .Where(g => g.Key.HasValue).ToDictionary(g => g.Key.Value, g => g.Count());

            if (groupedByMatchStatus.Any())
            {

                sportOverview.InPlay = groupedByMatchStatus.ContainsKey(MatchStatus.InRunning)
                    ? groupedByMatchStatus[MatchStatus.InRunning]
                    : 0;

                sportOverview.InPreMatch = groupedByMatchStatus.ContainsKey(MatchStatus.Prematch)
                    ? groupedByMatchStatus[MatchStatus.Prematch]
                    : 0;

                sportOverview.InSetup = groupedByMatchStatus.ContainsKey(MatchStatus.Setup)
                    ? groupedByMatchStatus[MatchStatus.Setup]
                    : 0;
            }

            if (_sportOverviews.ContainsKey(sportOverview.Name) &&
                _sportOverviews[sportOverview.Name].Equals(sportOverview))
            {
                return;
            }

            _sportOverviews[sportOverview.Name] = sportOverview;
            _sportTracker.OnNext(sportOverview);

        }

        public override void UpdateCurrentlyAvailableFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup)
        {
            base.UpdateCurrentlyAvailableFixtures(sport, currentfixturesLookup);

            //this ensures Supervisor removes all disposed stream listeners if even other mechanisms failed
            UpdateListenersList();
        }

        private void UpdateListenersList()
        {
            var activeListeners = GetListenersBySport().SelectMany(l => l).ToDictionary(l => l.FixtureId);
            
            //filter fixtures for which stream listener does not exist 
            foreach (var fixtureOverview in _fixtures.Values.Where(f=> f != null && f.Id != null && !activeListeners.ContainsKey(f.Id)))
            {
                CleanUpFixtureData(fixtureOverview.Id);
            }
        }

        private void UpdateStateFromStreamListener(StreamListener listener)
        {
            //Nothing to update
            if (listener == null)
                return;

            //this is accessing a dictionary object
            var fixtureOverview = GetFixtureOverview(listener.FixtureId) as FixtureOverview;

            fixtureOverview.Id = listener.FixtureId;
            fixtureOverview.Sport = listener.Sport;
            fixtureOverview.ListenerOverview.IsDeleted = listener.IsFixtureDeleted;
            fixtureOverview.ListenerOverview.IsStreaming = listener.IsStreaming;
            fixtureOverview.ListenerOverview.IsOver = listener.IsFixtureEnded;
            fixtureOverview.ListenerOverview.IsErrored = listener.IsErrored;

            fixtureOverview.TimeStamp = DateTime.UtcNow;
        }


        public IObservable<IFixtureOverviewDelta> GetFixtureOverviewStream()
        {
            return _fixtureTracker;
        }

        public IEnumerable<IFixtureOverview> GetFixtures()
        {
            return _fixtures.Values;
        }

        public IFixtureOverview GetFixtureOverview(string fixtureId)
        {
            return _fixtures.ContainsKey(fixtureId) ? _fixtures[fixtureId] : _fixtures[fixtureId] = new FixtureOverview(fixtureId);
        }

        private StreamListener GetStreamListenerObject(string fixtureId)
        {
            return GetStreamListener(fixtureId) as StreamListener;
        }

        public IObservable<IFixtureOverviewDelta> GetFixtureOverviewStream(string fixtureId)
        {
            return _fixtureTracker.Where(f => f.Id == fixtureId);
        }

        public IEnumerable<ISportOverview> GetSports()
        {
            return _sportOverviews.Values;
        }

        public ISportOverview GetSportOverview(string sportCode)
        {
            return _sportOverviews.ContainsKey(sportCode) ? _sportOverviews[sportCode] : null;
        }

        public IObservable<ISportOverview> GetSportOverviewStream(string sportCode)
        {
            return _sportTracker.Where(s => String.Equals(s.Name, sportCode, StringComparison.CurrentCultureIgnoreCase));
        }

        public IAdapterVersion GetAdapterVersion()
        {
            return AdapterVersionInfo.GetAdapterVersionInfo();
        }

        public IObservable<IFixtureOverviewDelta> GetAllFixtureOverviewStreams()
        {
            return _fixtureTracker;
        }

        public IObservable<IFixtureOverviewDelta> GetFixtureStreams(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return null;

            return _fixtureTracker.Where(f => f.Id == fixtureId);
        }

        public IObservable<ISportOverview> GetAllSportOverviewStreams()
        {
            return _sportTracker;
        }

        /// <summary>
        /// Stops stream listener and removed all Adapter state + plugin state
        /// </summary>
        /// <param name="fixtureId"></param>
        public void RemoveFixtureState(string fixtureId)
        {
            //we don't want to have stream listener running while we remove it's state
            StopStreaming(fixtureId);
            RemoveStreamListener(fixtureId);

            StateManager.ClearState(fixtureId);
            EventState.RemoveFixture(fixtureId);

            StateProviderProxy.StateProvider.RemovePluginState(fixtureId);
        }
        
        public void ForceListenerStop(string fixtureId)
        {
            StopStreaming(fixtureId);
            RemoveStreamListener(fixtureId);
        }
        
        private bool RemoveStreamListener(string fixtureId)
        {
            _logger.DebugFormat("Removing stream listener {0}", fixtureId);

            var result = base.RemoveStreamListener(fixtureId);
            CleanUpFixtureData(fixtureId);

            return result;
        }

        private void CleanUpFixtureData(string fixtureId)
        {
            var fixtureState = EventState.GetFixtureState(fixtureId);

            FixtureOverview tempObj = null;
            _fixtures.TryRemove(fixtureId, out tempObj);

            if (fixtureState != null && fixtureState.MatchStatus == MatchStatus.MatchOver)
            {
                SaveState();
            }
        }

        #region IDisposable

        public void Dispose()
        {
            _fixtureTracker.OnCompleted();
            _sportTracker.OnCompleted();

            if (Service != null)
                Service.Stop();

            Proxy.Dispose();

            SaveState();
        }

        #endregion
    }
}


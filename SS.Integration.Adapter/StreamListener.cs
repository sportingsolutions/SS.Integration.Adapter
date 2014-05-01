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
using System.Runtime.CompilerServices;
using System.Threading;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules;
using log4net;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Extensions;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Exceptions;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;


namespace SS.Integration.Adapter
{
    /// <summary>
    /// Instances of this class are meant to be used per resource (fixture) so
    /// one udapi's resource is linked to one instance of this class.
    /// Listener's events will run under udapi resource's thread
    /// therfore this class is not thread-safe but single threaded.
    /// </summary>
    public class StreamListener : IListener
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListener).ToString());

        private readonly IResourceFacade _resource;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IEventState _eventState;
        private readonly IObjectProvider<IMarketStateCollection> _stateProvider;
        private readonly IStatsHandle _Stats;
        
        private MarketsRulesManager _marketsRuleManager;

        private int _currentSequence;
        private int _currentEpoch;
        private int _lastSequenceProcessedInSnapshot;

        public StreamListener(IResourceFacade resource, IAdapterPlugin platformConnector, IEventState eventState, IObjectProvider<IMarketStateCollection> stateProvider)
        {

            if (resource == null)
                throw new ArgumentException("Resource information cannot be null", "resource");

            if (resource.Content == null)
                throw new Exception("Resource does not contain any content");


            _logger.DebugFormat("Instantiating StreamListener for {0} with sequence={1}", resource, resource.Content.Sequence);

            _resource = resource;
            _platformConnector = platformConnector;
            _eventState = eventState;
            _stateProvider = stateProvider;

            _currentSequence = resource.Content.Sequence;
            _lastSequenceProcessedInSnapshot = -1;
            _currentEpoch = -1;


            IsStreaming = false;
            IsConnecting = false;
            AllowsUpdates = false;

            IsErrored = false;
            IsIgnored = false;
            IsFixtureDeleted = false;
            
            IsFixtureEnded = _resource.IsMatchOver;
            IsFixtureSetup = (_resource.MatchStatus == MatchStatus.Setup ||
                              _resource.MatchStatus == MatchStatus.Ready);


            _Stats = StatsManager.Instance["StreamListener"].GetHandle(_resource.Id);
            _Stats.SetValue(StreamListenerKeys.FIXTURE, _resource.Id);
            _Stats.SetValue(StreamListenerKeys.STATUS, "Idle");

            SetupListener();
        }

        /// <summary>
        /// Returns true if the match is over and the fixture is ended.
        /// When the fixture is ended the listener will not pass any updates.
        /// 
        /// Thread-safe property
        /// </summary>
        public bool IsFixtureEnded 
        { 
            [MethodImpl(MethodImplOptions.Synchronized)]
            get; 
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set; 
        }

        /// <summary>
        /// Returns true if the current state of the listener
        /// is errored. In this case, if we are streaming
        /// (IsStreaming is true) then at the next update
        /// a snapshot will be acquired and processed instead
        /// of a delta snapshot. If the listener is not
        /// connected to the streaming server, then
        /// the listener for the resource must be re-created.
        /// 
        /// /// Thread-safe property
        /// </summary>
        public bool IsErrored 
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set; 
        }

        /// <summary>
        /// Returns true if the fixture is ignored.
        /// When a fixture is ignored, the listener will not
        /// push any updates.
        /// 
        /// Thread-safe property
        /// </summary>
        public bool IsIgnored 
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set; 
        }

        /// <summary>
        /// Returns true when the fixture is deleted.
        /// In this case, the listener has to be stopped
        /// as it has finished its job.
        /// 
        /// Thread-safe property
        /// </summary>
        public bool IsFixtureDeleted 
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set; 
        }

        /// <summary>
        /// Returns true if the fixture is not ready to receive
        /// updates. When IsFixtureSetup is true, the listener
        /// cannot be connected to the streaming server.
        /// 
        /// Thread-safe property
        /// </summary>
        public bool IsFixtureSetup 
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set; 
        }

        /// <summary>
        /// Returns true if the listener is connected
        /// to the streaming server.
        /// 
        /// Thread-safe property
        /// </summary>
        public bool IsStreaming 
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set; 
        }

        /// <summary>
        /// Returns true if the listener
        /// is in the process of connecting
        /// itself to the stream server.
        /// 
        /// Thread-safe property
        /// </summary>
        private bool IsConnecting
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        }

        /// <summary>
        /// If false, no updates can be processed.
        /// This is used to enforce synchronization
        /// while processing updates and snapshots
        /// 
        /// This is NOT a thred-safe property
        /// </summary>
        private bool AllowsUpdates
        {            
            get;            
            set;
        }

        /// <summary>
        /// Starts the listener. 
        /// </summary>
        public void Start()
        {
            StartStreaming();
        }

        public void UpdateResourceState(IResourceFacade resource)
        {
            IsFixtureSetup = (resource.MatchStatus == MatchStatus.Setup ||
                              resource.MatchStatus == MatchStatus.Ready);

            StartStreaming();
        }

        private void SetupListener()
        {
            _resource.StreamConnected += ResourceOnStreamConnected;
            _resource.StreamDisconnected += ResourceOnStreamDisconnected;
            _resource.StreamEvent += ResourceOnStreamEvent;
        }

        private void StartStreaming()
        {
            // do not start streaming twice
            if (IsStreaming && !IsErrored)
                return;

            if (IsFixtureEnded)
            {
                _logger.DebugFormat("Listener will not start for {0} as it is marked as ended", _resource);
                return;
            }

            IsErrored = false;
            IsStreaming = false;

            // Only start streaming if fixture is not Setup/Ready
            if (!IsFixtureSetup)
            {
                _logger.InfoFormat("{0} sport={1} starts streaming with sequence={2}", _resource, _resource.Sport, _currentSequence);
                ConnectToStreamServer();
            }
            else
            {
                _Stats.SetValue(StreamListenerKeys.STATUS, "Ready");
                _logger.InfoFormat(
                    "{0} is in Setup stage so the listener will not connect to streaming server whilst it's in this stage",
                    _resource);
            }
        }

        private void ConnectToStreamServer()
        {
            if (IsConnecting)
            {
                _logger.DebugFormat("Listener for {0} is already trying to connect - skipping request to connect to the streming server", _resource);
                return;
            }

            try
            {

                // The current UDAPI SDK, before consuming events from the queue raises
                // a "connected" event. We can then be sure that no updates are pushed
                // before our "connected" event handler terminates. In other words
                // the connected event handler doesn't create race conditions 
                // with th "stream" event handler. However, for not relying
                // on the internal details of the SDK (that can be changed at any time)
                // we put a synchronization mechanism enforcing the completition of
                // the "connected" event handler executing any updates via 
                // the "stream" event handler.

                _logger.DebugFormat("Starting streaming for {0}", _resource);
                
                IsConnecting = true;

                // enforce updates to wait 
                CloseProcessUpdatesBarrier();
                
                _resource.StartStreaming();
                _logger.DebugFormat("Streaming started for {0}", _resource);
            }
            catch (Exception ex)
            {
                IsErrored = true;
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _logger.Error(string.Format("An error has occured when trying to connect to stream server for {0}", _resource), ex);
            }
        }

        public void Stop()
        {
            _logger.InfoFormat("Stopping Listener for {0} sport={1}", _resource, _resource.Sport);

            CloseProcessUpdatesBarrier();

            if (IsErrored)
                SuspendMarkets();

            _resource.StreamConnected -= ResourceOnStreamConnected;
            _resource.StreamDisconnected -= ResourceOnStreamDisconnected;
            _resource.StreamEvent -= ResourceOnStreamEvent;

            if (IsStreaming)
            {
                _resource.StopStreaming();
            }
        }

        public void SuspendMarkets(bool fixtureLevelOnly = true)
        {            
            if (_marketsRuleManager == null)
                return;

            _logger.InfoFormat("Suspending Markets for {0} with fixtureLevelOnly={1}", _resource, fixtureLevelOnly);

            try
            {
                _platformConnector.Suspend(_resource.Id);
                if (!fixtureLevelOnly)
                {
                    var suspendedSnapshot = _marketsRuleManager.GenerateAllMarketsSuspenssion();
                    _platformConnector.ProcessStreamUpdate(suspendedSnapshot);
                }

            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("Error while suspending markets for {0}", _resource), innerException);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Error while suspending markets for {0}", _resource), ex);
            }
        }

        /// <summary>
        /// Check if an update can be processed.
        /// If it cannot be processed the calling
        /// thread waits until the barrier will
        /// be opened
        /// </summary>
        private void HitProcessUpdatesBarrier()
        {
            lock (this)
            {
                while (!AllowsUpdates)
                {
                    Monitor.Wait(this);
                }
            }
        }

        /// <summary>
        /// Open the updates barrier. In other words
        /// updates can be processed. All the waiting
        /// threads are waked up. 
        /// 
        /// Please note that no threads ordering
        /// is enfored when the barrier is opened
        /// </summary>
        private void OpenProcessUpdatesBarrier()
        {
            lock (this)
            {
                AllowsUpdates = true;
                Monitor.PulseAll(this);
            }
        }

        /// <summary>
        /// A call to this method allows all the updates
        /// to wait until OpenProcessUpdatesBarrier()
        /// is called.
        /// 
        /// Pay attention that if multiple threads
        /// are waiting on the barrier, when this is opened
        /// no threads ordering is enforced. 
        /// 
        /// </summary>
        private void CloseProcessUpdatesBarrier()
        {
            lock (this)
            {
                AllowsUpdates = false;
            }
        }

        private bool CheckErrorStateOnUpdates()
        {
            if (IsErrored)
            {
                IsErrored = false;

                SuspendAndReprocessSnapshot();

                return true;
            }

            return false;
        }

        public void ResourceOnStreamEvent(object sender, StreamEventArgs streamEventArgs)
        {
            try
            {
                // hit the barrier to enforce synchronization
                HitProcessUpdatesBarrier();
                
                // grab a snapshot and return if there was an error
                if (CheckErrorStateOnUpdates())
                    return;


                var deltaMessage = streamEventArgs.Update.FromJson<StreamMessage>();
                var fixtureDelta = deltaMessage.GetContent<Fixture>();

                _logger.InfoFormat("{0} streaming update arrived", fixtureDelta);

                if (!IsSequenceValid(fixtureDelta))
                {
                    _logger.InfoFormat("Stream update {0} will not be processed because sequence was not valid", fixtureDelta);
                    
                    _Stats.SetValue(StreamListenerKeys.LAST_INVALID_SEQUENCE, fixtureDelta.Sequence);
                    _Stats.IncrementValue(StreamListenerKeys.INVALID_SEQUENCE);

                    // if snapshot was already processed with higher sequence no need to process this sequence
                    // THIS should never happen!!
                    if (fixtureDelta.Sequence <= _lastSequenceProcessedInSnapshot)
                    {
                        _logger.FatalFormat(
                            "Stream update {0} will be ignored because snapshot with higher sequence={1} was already processed",
                            fixtureDelta, _lastSequenceProcessedInSnapshot);
                        return;
                    }

                    SuspendAndReprocessSnapshot();
                    return;
                }

                bool hasEpochChanged;
                var epochValid = IsEpochValid(fixtureDelta, out hasEpochChanged);

                if (epochValid)
                {
                    ProcessSnapshot(fixtureDelta, false, hasEpochChanged);
                }
                else
                {

                    if (fixtureDelta.IsMatchStatusChanged && !string.IsNullOrEmpty(fixtureDelta.MatchStatus))
                    {
                        _logger.InfoFormat("{0} has changed matchStatus={1}", _resource, Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus));
                        _platformConnector.ProcessMatchStatus(fixtureDelta);
                    }

                    if ((fixtureDelta.IsMatchStatusChanged && fixtureDelta.IsMatchOver) || fixtureDelta.IsDeleted)
                    {

                        if (fixtureDelta.IsDeleted)
                        {
                            ProcessFixtureDelete(fixtureDelta);
                        }
                        else  // Match Over
                        {
                            ProcessMatchOver(fixtureDelta);
                        }

                        Stop();
                        return;
                    }


                    _logger.InfoFormat("Stream update {0} will not be processed because epoch was not valid", fixtureDelta);
                    _Stats.SetValue(StreamListenerKeys.LAST_INVALID_SEQUENCE, fixtureDelta.Sequence);
                    _Stats.IncrementValue(StreamListenerKeys.INVALID_EPOCH);

                    SuspendAndReprocessSnapshot(hasEpochChanged);
                    return;
                }

                _logger.InfoFormat("{0} streaming Update was processed successfully", fixtureDelta);
                _Stats.SetValue(StreamListenerKeys.LAST_SEQUENCE, fixtureDelta.Sequence);
                _Stats.IncrementValue(StreamListenerKeys.UPDATE_PROCESSED);
            }
            catch (AggregateException ex)
            {
                IsErrored = true;
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), innerException);
                    _Stats.AddMessage(GlobalKeys.CAUSE, innerException);
                }

                _Stats.SetValue(StreamListenerKeys.STATUS, "Error");
            }
            catch (Exception ex)
            {
                IsErrored = true;
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), ex);
            }
        }

        public void ResourceOnStreamDisconnected(object sender, EventArgs eventArgs)
        {
            IsStreaming = false;

            if (!this.IsFixtureEnded)
            {
                IsErrored = true;

                _logger.WarnFormat("Stream disconnected for {0}, suspending markets, will try reconnect soon", _resource);
                _Stats.AddMessage(GlobalKeys.CAUSE, "Stream disconnected").SetValue(StreamListenerKeys.STATUS, "Error");

                SuspendMarkets();
            }
            else
            {
                _Stats.AddMessage(GlobalKeys.CAUSE, "Fixture is over").SetValue(StreamListenerKeys.STATUS, "Disconnected");
                _logger.InfoFormat("Stream disconnected for {0} - fixture is over", _resource);
            }
        }

        public void ResourceOnStreamConnected(object sender, EventArgs eventArgs)
        {
            IsStreaming = true;
            IsConnecting = false;

            _logger.InfoFormat("Stream connected for {0}", _resource);
            _Stats.SetValue(StreamListenerKeys.STATUS, "Connected");
            _Stats.IncrementValue(StreamListenerKeys.RESTARTED);

            try
            {
                RetrieveAndProcessSnapshotIfNeeded();
            }
            finally
            {
                // first snapshot is processed....if no error
                // was raised, allows updates to be processed
                OpenProcessUpdatesBarrier();
            }
        }

        private bool IsSequenceValid(Fixture fixtureDelta)
        {
            if(fixtureDelta.Sequence < _currentSequence)
            {
                _logger.InfoFormat("sequence={0} is less than currentSequence={1} in {2}", 
                    fixtureDelta.Sequence, _currentSequence, fixtureDelta);
                return false;
            }
            
            if (fixtureDelta.Sequence > _currentSequence)
            {
                _logger.WarnFormat("equence={0} is more than one greater that currentSequence={1} in {2} ", 
                    fixtureDelta.Sequence, _currentSequence, fixtureDelta);
                return false;
            }

            _currentSequence = fixtureDelta.Sequence;

            return true;
        }

        private bool IsEpochValid(Fixture fixtureDelta, out bool hasEpochChanged)
        {
            hasEpochChanged = fixtureDelta.Epoch != this._currentEpoch;

            if (fixtureDelta.Epoch < _currentEpoch)
            {
                _logger.WarnFormat("Unexpected Epoch={0} when current={1} for {2}", fixtureDelta.Epoch, _currentEpoch, _resource);
                IsErrored = true;
                return false;
            }
            
            if (fixtureDelta.Epoch == _currentEpoch)
                return true;

            // Cases for fixtureDelta.Epoch > _currentEpoch
            _logger.InfoFormat("Epoch changed for {0} from={1} to={2}", _resource, _currentEpoch, fixtureDelta.Epoch);

            _currentEpoch = fixtureDelta.Epoch;

            if (fixtureDelta.IsStartTimeChanged)
            {
                _logger.InfoFormat("{0} has had its start time changed", _resource);
                return true;
            }

            return false;
        }

        /// <summary>
        /// This method allows to grab a new snapshot and process it.
        /// 
        /// Before getting the snapshot, it closes the updates barrier
        /// (so no updates can be process meanwhile) and suspends
        /// all the fixture's markets. After the snapshot is processed
        /// the updates barrier is opened again.
        /// </summary>
        /// <param name="hasEpochChanged"></param>
        private void SuspendAndReprocessSnapshot(bool hasEpochChanged = false)
        {
            CloseProcessUpdatesBarrier();

            try
            {
                SuspendMarkets();
                RetrieveAndProcessSnapshot(hasEpochChanged);
            }
            finally
            {
                OpenProcessUpdatesBarrier();
            }
        }

        private Fixture RetrieveSnapshot()
        {
            _logger.DebugFormat("Getting Snapshot for {0}", _resource);

            try
            {
                var snapshotJson = _resource.GetSnapshot();

                if (string.IsNullOrEmpty(snapshotJson))
                    throw new Exception("Received empty snapshot");

                return FixtureJsonHelper.GetFromJson(snapshotJson);
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An error occured while trying to acquire snapshot for {0}", _resource), e);
                IsErrored = true;
            }

            return null;
        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false)
        {
            var snapshot = RetrieveSnapshot();
            if (snapshot != null)
                ProcessSnapshot(snapshot, true, hasEpochChanged);
        }

        private void RetrieveAndProcessSnapshotIfNeeded()
        {
            FixtureState state = _eventState.GetFixtureState(_resource.Id);
            int sequence_number = -1;
            if(state != null)
                sequence_number = state.Sequence;
            

            _logger.DebugFormat("{0} has stored sequence={1} and current_sequence={2}", _resource, sequence_number, _currentSequence);

            if (sequence_number == -1 || _currentSequence != sequence_number)
            {
                RetrieveAndProcessSnapshot();
            }
            else
            {
                Fixture fixture = new Fixture {Sequence = sequence_number, Id = _resource.Id};

                if (state != null)
                    fixture.MatchStatus = state.MatchStatus.ToString();

                try
                {
                    _platformConnector.UnSuspend(fixture);
                }
                catch (Exception e)
                {
                    IsErrored = true;
                    _logger.Error(string.Format("An error occcured while trying to unsuspend {0}", _resource), e);
                }
            }
        }

        private void ProcessSnapshot(Fixture snapshot, bool isFullSnapshot, bool hasEpochChanged)
        {
            _logger.DebugFormat("Processing snapshot for {0} with isFullSnapshot={1}", snapshot, isFullSnapshot);

            try
            {
                _logger.DebugFormat("Applying market rules for {0}", _resource);

                ApplyMarketRules(snapshot);

                _logger.DebugFormat("Sending snapshot for {0} to plugin with has_epoch_changed={1}", snapshot, hasEpochChanged);

                if (isFullSnapshot)
                    _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);
                else
                    _platformConnector.ProcessStreamUpdate(snapshot, hasEpochChanged);

                UpdateState(snapshot, isFullSnapshot);
            }
            catch (FixtureIgnoredException ie)
            {
                _logger.InfoFormat("{0} received a FixtureIgnoredException. Stopping listener", _resource);
                _logger.Error("FixtureIgnoredException", ie);
                IsIgnored = true;
                Stop();
            }
            catch (AggregateException ex)
            {
                IsErrored = true;
                _marketsRuleManager.RollbackChanges();

                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("There has been an aggregate error while trying to process snapshot {0}", snapshot), innerException);
                }

                
            }
            catch (Exception e)
            {
                IsErrored = true;
                _marketsRuleManager.RollbackChanges();

                _logger.Error(string.Format("An error occured while trying to process snapshot {0}", snapshot), e);
            }
        }

        private void UpdateState(Fixture snapshot, bool isSnapshot = false)
        {
            _logger.DebugFormat("Updating state for {0} with isSnapshot={1}", _resource, isSnapshot);

            _marketsRuleManager.CommitChanges();

            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), snapshot.MatchStatus);

            _eventState.UpdateFixtureState(_resource.Sport, _resource.Id, snapshot.Sequence, status);

            if (isSnapshot)
            {
                _lastSequenceProcessedInSnapshot = snapshot.Sequence;
                _currentEpoch = snapshot.Epoch;
                _Stats.IncrementValue(StreamListenerKeys.SNAPSHOT_RETRIEVED);
            }

            _currentSequence = snapshot.Sequence;
            _Stats.SetValue(StreamListenerKeys.LAST_SEQUENCE, _currentSequence);
        }

        private void ApplyMarketRules(Fixture fixture)
        {
            if (_marketsRuleManager == null)
            {
                _logger.DebugFormat("Instantiating market rule manager for {0}", _resource);

                List<IMarketRule> rules = new List<IMarketRule> 
                { 
                    InactiveMarketsFilteringRule.Instance,
                    VoidUnSettledMarket.Instance
                };

                rules.AddRange(_platformConnector.MarketRules);
                rules.Reverse();

                _marketsRuleManager = new MarketsRulesManager(fixture, _stateProvider, rules);
            }

            _marketsRuleManager.ApplyRules(fixture);
        }

        public bool CheckStreamHealth(int maxPeriodWithoutMessage, int receivedSequence = -1)
        {
            if (IsFixtureSetup || !IsStreaming || IsFixtureDeleted)
            {
                // Stream has not yet started as fixture is Setup/Ready
                return true;
            }

            /*var shouldProcessSnapshot = (_sequenceSynchroniser == null || _sequenceSynchroniser.IsCompleted)
                && _currentSequence != -1
                && _currentSequence < receivedSequence
                && _lastSequenceProcessedInSnapshot < receivedSequence;

            if (shouldProcessSnapshot && !IsFixtureDeleted && !IsErrored && !_isUpdateBeingProcessed)
            {
                _logger.WarnFormat("Received sequence different from expected sequence {0} receivedSequence={1} currentSequence={2}", _fixtureSnapshot, receivedSequence, _currentSequence);
                if (_sequenceSynchroniser == null || _sequenceSynchroniser.IsCompleted)
                    _sequenceSynchroniser = Task.Factory.StartNew(() => SuspendAndReprocessSnapshot());
                else
                {
                    _logger.DebugFormat("The sequence synchroniser is already running for fixture {0}", _fixtureSnapshot);
                }
            }*/

            var streamStatistics = _resource as IStreamStatistics;

            // No message has ever been received
            if (streamStatistics == null || streamStatistics.LastMessageReceived == DateTime.MinValue)
            {
                return true;
            }

            var timespan = DateTime.UtcNow - streamStatistics.LastMessageReceived;
            if (timespan.TotalMilliseconds >= maxPeriodWithoutMessage)
            {
                IsErrored = true;
                _logger.WarnFormat("Stream for {0} has not received a message in span={1}, suspending markets, will try to reconnect soon", _resource.Id, timespan.TotalSeconds);
                SuspendMarkets();
                return false;
            }

            return true;
        }

        private void ProcessFixtureDelete(Fixture fixtureDelta)
        {
            IsFixtureDeleted = true;

            _logger.InfoFormat("{0} has been deleted from the GTP Fixture Factory. Suspending all markets and stopping the stream.", _resource);

            SuspendMarkets(false);

            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus);
            
            //reset event state
            _eventState.UpdateFixtureState(_resource.Sport, fixtureDelta.Id, -1, status);
        }

        private void ProcessMatchOver(Fixture fixtureDelta)
        {
            _logger.InfoFormat("{0} is Match Over. Suspending all markets and stopping the stream.", _resource);

            try
            {
                SuspendAndReprocessSnapshot(true);
                _platformConnector.ProcessFixtureDeletion(fixtureDelta);

                this.IsFixtureEnded = true;
            }
            catch (Exception e)
            {
                IsErrored = true;
                _logger.Error(string.Format("An error occured while trying to process match over snapshot {0}", fixtureDelta), e);
            }
        }
    
    }
}

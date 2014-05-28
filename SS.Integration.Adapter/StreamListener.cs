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
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules;
using log4net;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Extensions;
using SS.Integration.Adapter.MarketRules.Interfaces;
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
        private readonly IObjectProvider<IUpdatableMarketStateCollection> _stateProvider;
        private readonly IStatsHandle _Stats;
        
        private MarketsRulesManager _marketsRuleManager;

        private int _currentSequence;
        private int _currentEpoch;
        private int _lastSequenceProcessedInSnapshot;
        private bool _hasRecoveredFromError;
        private bool _isFirstSnapshotProcessed;

        public StreamListener(IResourceFacade resource, IAdapterPlugin platformConnector, IEventState eventState, IObjectProvider<IUpdatableMarketStateCollection> stateProvider)
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
            _hasRecoveredFromError = true;
            _isFirstSnapshotProcessed = false;

            IsStreaming = false;
            IsConnecting = false;

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
        /// This property is set when a snapshot says that the fixture
        /// is over. The streaming stops when such a snapshot arrives.
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
        /// as its job is finished
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
        /// Use UpdateResourceState() to update the
        /// resource's state and start the streaming
        /// if necessary
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
        /// Returns true if the current state of the listener
        /// is errored. In this case, if we are streaming
        /// (IsStreaming is true) then at the next update
        /// a snapshot will be acquired and processed instead
        /// of a delta snapshot. 
        /// 
        /// Please note that this indicates only that 
        /// an error occured while streaming.
        /// 
        /// Thread-safe property
        /// </summary>
        private bool IsErrored
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        }

        /// <summary>
        /// Starts the listener. 
        /// </summary>
        public void Start()
        {
            StartStreaming();
        }

        /// <summary>
        /// Stops the streaming
        /// </summary>
        public void Stop()
        {
            _logger.InfoFormat("Stopping Listener for {0} sport={1}", _resource, _resource.Sport);

            if (IsErrored)
                SuspendMarkets();

            if (IsStreaming)
            {
                _resource.StreamConnected -= ResourceOnStreamConnected;
                _resource.StreamDisconnected -= ResourceOnStreamDisconnected;
                _resource.StreamEvent -= ResourceOnStreamEvent;

                _resource.StopStreaming();
            }
        }

        /// <summary>
        /// Allows to inform this object that a resource
        /// may have changed its status.
        /// 
        /// If the resource is now in a state were
        /// streaming is allowed, this object will 
        /// try to connect to the streaming server.
        /// </summary>
        /// <param name="resource"></param>
        public void UpdateResourceState(IResourceFacade resource)
        {
            IsFixtureSetup = (resource.MatchStatus == MatchStatus.Setup ||
                              resource.MatchStatus == MatchStatus.Ready);

            StartStreaming();
        }

        /// <summary>
        /// Allows to send a suspension requests for all
        /// the markets associated to the fixture.
        ///        
        /// </summary>
        /// <param name="fixtureLevelOnly">If true, the object will send
        /// a fixture suspend request instead of a set of market suspend requests
        /// </param>
        public void SuspendMarkets(bool fixtureLevelOnly = true)
        {
            _logger.InfoFormat("Suspending Markets for {0} with fixtureLevelOnly={1}", _resource, fixtureLevelOnly);

            try
            {
                _platformConnector.Suspend(_resource.Id);
                if (!fixtureLevelOnly)
                {
                    if (_marketsRuleManager == null)
                    {
                        _logger.WarnFormat("Cannot perform a full suspension of {0} as no state information is currently available", _resource);
                        return;
                    }


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

        private void SetupListener()
        {
            _resource.StreamConnected += ResourceOnStreamConnected;
            _resource.StreamDisconnected += ResourceOnStreamDisconnected;
            _resource.StreamEvent += ResourceOnStreamEvent;
        }

        private void StartStreaming()
        {
  
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

                // even if we are in setup, we still want to process a snapshot
                // in order to insert the fixture
                ProcessFirstSnapshotIfNecessary();
            }
        }

        private void ConnectToStreamServer()
        {
            // even if each property used within this block is thread-safe
            // we need to use a lock block because we want to do a check-set operation
            lock (this)
            {
                if (IsFixtureEnded)
                {
                    _logger.DebugFormat("Listener will not start for {0} as it is marked as ended", _resource);
                    return;
                }

                // do not start streaming twice
                if (IsStreaming || IsConnecting)
                    return;

                IsErrored = false;
                IsStreaming = false;
                IsConnecting = true;
            }

            try
            {

                // The current UDAPI SDK, before consuming events from the queue raises
                // a "connected" event. We are sure that no updates are pushed
                // before our "connected" event handler terminates because 
                // updates are pushed using the same thread the SDK uses to 
                // push updates.
                //
                // If the SDK's threading model changes, this 
                // class must be revisited

                _logger.DebugFormat("Starting streaming for {0}", _resource);
                _resource.StartStreaming();
                _logger.DebugFormat("Streaming started for {0}", _resource);
            }
            catch (Exception ex)
            {
                IsConnecting = false;
                IsStreaming = false;

                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _logger.Error(string.Format("An error has occured when trying to connect to stream server for {0}", _resource), ex);
            }
        }

        private void SetErrorState()
        {
            // the idea is that when we encounter an error
            // we immediately suspend the fixture and get
            // a new full snapshot to process.
            //
            // However, processing the new snapshot
            // might introduce a new error (and this
            // method is called again). For not
            // entering a useless loop, first thing we do
            // when we enter this method is checking
            // if IsError = true. If it is so, it means
            // that a new error was raised while processing
            // the snapshot that was acquired to try to solve
            // a previous error (got it? )
            //
            // If IsError = false, then we grab and process
            // a new snapshot. After this, due the fact
            // that processing the snapshot can raise a new 
            // error, we set IsError to !_hasRecoveredFromError
            // that is false only if a second call to 
            // SetErrorState was made

            if (IsErrored)
            {
                _hasRecoveredFromError = false;

                // make sure markets are suspended
                SuspendMarkets(); 
                _logger.ErrorFormat("Streming for {0} is still in an error state even after trying to recover with a full snapshot", _resource);
                return;
            }

            _logger.InfoFormat("Streaming for {0} entered the error state - going to acquire a new snapshot", _resource);

            _hasRecoveredFromError = true;
            IsErrored = true;
            SuspendAndReprocessSnapshot();
            IsErrored = !_hasRecoveredFromError;
        }

        internal void ResourceOnStreamEvent(object sender, StreamEventArgs streamEventArgs)
        {
            // Note that we can have only one update at time
            // as the current SDK's threading model calls this 
            // method using always the same thread (per object basis)

            try
            {
             
                var deltaMessage = streamEventArgs.Update.FromJson<StreamMessage>();
                var fixtureDelta = deltaMessage.GetContent<Fixture>();

                _logger.InfoFormat("{0} streaming update arrived", fixtureDelta);

                // if there was an error from which we haven't recovered yet
                // it might be that with a new sequence, we might recover.
                // So in this case, ignore the update and grab a new 
                // snapshot.
                if (IsErrored)
                {
                    _logger.InfoFormat("Streaming was in an error state for {0} - skipping update and grabbing a new snapshot", _resource);
                    IsErrored = false;
                    RetrieveAndProcessSnapshot();
                    return;
                }

                

                if (!IsSequenceValid(fixtureDelta))
                {
                    _logger.InfoFormat("Stream update {0} will not be processed because sequence was not valid", fixtureDelta);
                    
                    _Stats.SetValue(StreamListenerKeys.LAST_INVALID_SEQUENCE, fixtureDelta.Sequence);
                    _Stats.IncrementValue(StreamListenerKeys.INVALID_SEQUENCE);

                    // if snapshot was already processed with higher sequence no need to process this sequence
                    // THIS should never happen!!
                    if (fixtureDelta.Sequence <= _lastSequenceProcessedInSnapshot)
                    {
                        _logger.FatalFormat("Stream update {0} will be ignored because snapshot with higher sequence={1} was already processed",
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
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), innerException);
                    _Stats.AddMessage(GlobalKeys.CAUSE, innerException);
                }

                _Stats.SetValue(StreamListenerKeys.STATUS, "Error");
                SetErrorState();
            }
            catch (Exception ex)
            {
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), ex);
                SetErrorState();
            }
        }

        internal void ResourceOnStreamDisconnected(object sender, EventArgs eventArgs)
        {
            IsStreaming = false;

            // for when a disconnect event is raised due a failed attempt to connect 
            // (in other words, when we didn't receive a connect event)
            IsConnecting = false;

            if (!this.IsFixtureEnded)
            {

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

        internal void ResourceOnStreamConnected(object sender, EventArgs eventArgs)
        {
            IsStreaming = true;
            IsConnecting = false;

            // we are connected, so we don't need the acquire the first snapshot
            // directly
            _isFirstSnapshotProcessed = true; 

            _logger.InfoFormat("Stream connected for {0}", _resource);
            _Stats.SetValue(StreamListenerKeys.STATUS, "Connected");
            _Stats.IncrementValue(StreamListenerKeys.RESTARTED);

            RetrieveAndProcessSnapshotIfNeeded();
        }

        private bool IsSequenceValid(Fixture fixtureDelta)
        {
            if(fixtureDelta.Sequence < _currentSequence)
            {
                _logger.InfoFormat("sequence={0} is less than currentSequence={1} in {2}", 
                    fixtureDelta.Sequence, _currentSequence, fixtureDelta);
                return false;
            }
            
            if (fixtureDelta.Sequence - _currentSequence > 1)
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
        /// Before getting the snapshot, it suspends
        /// all the fixture's markets. 
        /// </summary>
        /// <param name="hasEpochChanged"></param>
        private void SuspendAndReprocessSnapshot(bool hasEpochChanged = false)
        {
            SuspendMarkets();
            RetrieveAndProcessSnapshot(hasEpochChanged);
        }

        private Fixture RetrieveSnapshot(bool setErrorState = true)
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

                if(setErrorState)
                    SetErrorState();
            }

            return null;
        }

        private void ProcessFirstSnapshotIfNecessary()
        {
            // if we have already processed the first 
            // snapshot directly, don't do it again
            if (_isFirstSnapshotProcessed)
                return;

            try 
            {
                ProcessSnapshot(RetrieveSnapshot(false), true, false, false);
                _isFirstSnapshotProcessed = true;
            }
            catch 
            {
                // No need to raise up the exception
                // we will try again later
            }
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
                    _logger.Error(string.Format("An error occcured while trying to unsuspend {0}", _resource), e);
                    SetErrorState();
                }
            }
        }

        private void ProcessSnapshot(Fixture snapshot, bool isFullSnapshot, bool hasEpochChanged, bool setErrorState = true)
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
            }
            catch (AggregateException ex)
            {
                _marketsRuleManager.RollbackChanges();

                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("There has been an aggregate error while trying to process snapshot {0}", snapshot), innerException);
                }

                if(setErrorState)
                    SetErrorState();
            }
            catch (Exception e)
            {
                _marketsRuleManager.RollbackChanges();

                _logger.Error(string.Format("An error occured while trying to process snapshot {0}", snapshot), e);

                if(setErrorState)
                    SetErrorState();
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

                _marketsRuleManager = new MarketsRulesManager(fixture, _stateProvider, rules);
            }

            _marketsRuleManager.ApplyRules(fixture);
        }

        public bool CheckStreamHealth(int maxPeriodWithoutMessage, int receivedSequence)
        {
            if (IsFixtureSetup || IsFixtureDeleted)
            {
                // Stream has not yet started as fixture is Setup/Ready
                return true;
            }

            if (!IsStreaming)
                return false;

            var streamStatistics = _resource as IStreamStatistics;

            // No message has ever been received
            if (streamStatistics == null || streamStatistics.LastMessageReceived == DateTime.MinValue)
            {
                return true;
            }

            var timespan = DateTime.UtcNow - streamStatistics.LastMessageReceived;
            if (timespan.TotalMilliseconds >= maxPeriodWithoutMessage)
            {
                _logger.WarnFormat("Stream for {0} has not received a message in span={1}, suspending markets, will try to reconnect soon", 
                    _resource.Id, timespan.TotalSeconds);
                SuspendMarkets();
                return false;
            }

            return true;
        }

        private void ProcessFixtureDelete(Fixture fixtureDelta)
        {
            IsFixtureDeleted = true;

            _logger.InfoFormat("{0} has been deleted from the GTP Fixture Factory. Suspending all markets and stopping the stream.", _resource);

            try
            {
                SuspendMarkets(false);
                _platformConnector.ProcessFixtureDeletion(fixtureDelta);
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An exception occured while trying to process fixture deletion for {0}", _resource), e);
            }
            

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

                this.IsFixtureEnded = true;
                
                if(_marketsRuleManager != null)
                    _marketsRuleManager.Clear();
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An error occured while trying to process match over snapshot {0}", fixtureDelta), e);
            }
        }
    
    }
}

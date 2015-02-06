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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;
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

        private IResourceFacade _resource;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IEventState _eventState;
        private readonly IStateManager _stateManager;
        private readonly IStatsHandle _Stats;
        private readonly IMarketRulesManager _marketsRuleManager;
        private readonly ISettings _settings;

        private int _currentSequence;
        private int _currentEpoch;
        private int _lastSequenceProcessedInSnapshot;
        private bool _hasRecoveredFromError;
        private bool _isFirstSnapshotProcessed;
        private bool _isProcessingFirstSnapshot;
        private bool _performingDelayedStop;

        public StreamListener(IResourceFacade resource, IAdapterPlugin platformConnector, IEventState eventState, IStateManager stateManager, ISettings settings)
        {

            if (resource == null)
                throw new ArgumentException("Resource information cannot be null", "resource");

            if (resource.Content == null)
                throw new Exception("Resource does not contain any content");


            _logger.DebugFormat("Instantiating Listener for {0} with sequence={1}", resource, resource.Content.Sequence);

            _resource = resource;
            _platformConnector = platformConnector;
            _eventState = eventState;
            _stateManager = stateManager;
            _settings = settings;

            _currentSequence = resource.Content.Sequence;
            _lastSequenceProcessedInSnapshot = -1;
            _currentEpoch = -1;
            _hasRecoveredFromError = true;
            _isFirstSnapshotProcessed = false;
            _isProcessingFirstSnapshot = false;
            _performingDelayedStop = false;

            _marketsRuleManager = stateManager.CreateNewMarketRuleManager(resource.Id);

            FixtureId = resource.Id;
            Sport = resource.Sport;
            SequenceOnStreamingAvailable = _currentSequence;

            IsStreaming = false;
            IsConnecting = false;
            IsDisposing = false;
            IsErrored = false;
            IsIgnored = false;
            IsStopping = false;

            var fixtureState = _eventState.GetFixtureState(resource.Id);

            IsFixtureEnded = fixtureState != null ? fixtureState.MatchStatus == MatchStatus.MatchOver : _resource.IsMatchOver;
            IsFixtureSetup = (_resource.MatchStatus == MatchStatus.Setup || _resource.MatchStatus == MatchStatus.Ready);
            IsFixtureDeleted = false;
            IsInPlay = fixtureState != null ? fixtureState.MatchStatus == MatchStatus.InRunning : _resource.MatchStatus == MatchStatus.InRunning;

            _Stats = StatsManager.Instance[string.Concat("adapter.core.sport.", resource.Sport)].GetHandle();

            SetupListener();
            _logger.DebugFormat("Listener instantiated for {0}", resource);

        }


        public string FixtureId
        {
            get;
            private set;
        }

        public bool IsDisconnected
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set;
        }

        public bool IsInPlay
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set;
        }

        public string Sport { get; private set; }

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
        /// Returns true if the listener is in the process of stopping a stream.
        /// It is used to prevent establishing a connection to the stream server while
        /// the disconnect event handler is being executed.
        /// 
        /// Thread-safe property.
        /// </summary>
        internal bool IsStopping
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
        internal bool IsConnecting
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
        /// of a delta snapshot. 
        /// 
        /// Please note that this indicates only that 
        /// an error occured while streaming.
        /// 
        /// Thread-safe property
        /// </summary>
        internal bool IsErrored
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            private set;
        }

        /// <summary>
        /// This returns the sequence number of the 
        /// fixture when it can start streaming
        /// (needed to check if we need to acquire a new
        /// snapshot when connecting to the streaming
        /// server)
        /// </summary>
        internal int SequenceOnStreamingAvailable
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        }

        /// <summary>
        /// Returns true if the listener is in the process of being disposed.
        /// It is used to prevent sending more than one time the suspendion
        /// command.
        /// 
        /// Thread-safe property.
        /// </summary>
        private bool IsDisposing
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set;
        }

        /// <summary>
        /// Starts the listener. 
        /// </summary>
        public bool Start()
        {
            if (_resource == null)
                throw new ObjectDisposedException("StreamListener");

            StartStreaming();

            return !IsErrored;
        }

        /// <summary>
        /// Stops the streaming
        /// </summary>
        public void Stop()
        {
            if (_resource == null)
                return;

            _logger.InfoFormat("Stopping Listener for {0} sport={1}", FixtureId, _resource.Sport);

            if (IsStreaming)
            {
                _resource.StreamConnected -= ResourceOnStreamConnected;
                _resource.StreamDisconnected -= ResourceOnStreamDisconnected;
                _resource.StreamEvent -= ResourceOnStreamEvent;

                IsStreaming = false;
                IsConnecting = false;

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
            // this is the case when the StreamListener 
            // has been already stopped
            if (_resource == null)
                return;


            IsFixtureSetup = (resource.MatchStatus == MatchStatus.Setup ||
                              resource.MatchStatus == MatchStatus.Ready);

            SequenceOnStreamingAvailable = resource.Content.Sequence;

            _logger.InfoFormat("In feed there is a sequence={0} processedSequence={1} isDisconnected={2} isStreaming={3} {4}", SequenceOnStreamingAvailable, _currentSequence,IsDisconnected,IsStreaming, resource);

            StartStreaming();
        }

        private void Unsuspend()
        {
            _stateManager.StateProvider.SuspensionManager.Unsuspend(FixtureId);
        }

        /// <summary>
        /// Allows to send a suspension request
        ///        
        /// </summary>
        /// <param name="reason">The reason of the suspension</param>
        private void SuspendFixture(SuspensionReason reason)
        {
            _logger.InfoFormat("Suspending fixtureId={0} due reason={1}", FixtureId, reason);

            _stateManager.StateProvider.SuspensionManager.Suspend(FixtureId, reason);
        }

        private void SetupListener()
        {
            _resource.StreamConnected += ResourceOnStreamConnected;
            _resource.StreamDisconnected += ResourceOnStreamDisconnected;
            _resource.StreamEvent += ResourceOnStreamEvent;
        }

        private void StartStreaming()
        {
            if (IsDisposing || IsStopping)
            {
                _logger.InfoFormat("Start streaming requested by adapter for fixture {0} but it won't be executed because the stream listener is shutting down. ", _resource);
                return;
            }

            // Only start streaming if fixture is not Setup/Ready
            if (!IsFixtureSetup)
            {
                _logger.DebugFormat("{0} sport={1} attempts to start streaming", _resource, _resource.Sport);
                ConnectToStreamServer();
            }
            else
            {

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
            int sequence = -1;

            // even if each property used within this block is thread-safe
            // we need to use a lock block because we want to do a check-set operation
            lock (this)
            {
                if (IsFixtureEnded)
                {
                    _logger.DebugFormat("Listener will not start for {0} as the resource is marked as ended", _resource);
                    return;
                }

                // do not start streaming twice
                if (IsStreaming || IsConnecting)
                {
                    _logger.DebugFormat("Listener will not start for {0} as it is already streaming/connecting", _resource);
                    return;
                }

                IsErrored = false;
                IsStreaming = false;
                IsStopping = false;
                IsDisposing = false;
                IsConnecting = true;
                sequence = SequenceOnStreamingAvailable;
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

                _logger.DebugFormat("Starting streaming for {0} - resource has sequence={1}", _resource, sequence);
                _resource.StartStreaming();
            }
            catch (Exception ex)
            {
                IsConnecting = false;
                IsStreaming = false;
                IsErrored = true;

                _logger.Error(string.Format("An error has occured when trying to connect to stream server for {0}. Stream listener is market as errored.", _resource), ex);
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

            try
            {
                if (IsErrored)
                {
                    _hasRecoveredFromError = false;

                    // make sure markets are suspended
                    SuspendFixture(SuspensionReason.FIXTURE_ERRORED);
                    _logger.ErrorFormat("Streaming for {0} is still in an error state even after trying to recover with a full snapshot", _resource);
                    return;
                }

                _logger.DebugFormat("Streaming for {0} entered the error state - going to acquire a new snapshot", _resource);

                _hasRecoveredFromError = true;
                IsErrored = true;
                SuspendAndReprocessSnapshot();
                IsErrored = !_hasRecoveredFromError;
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Error while trying to recover from previous error. {0}", ex);
            }
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

                _logger.InfoFormat("{0} stream update arrived", fixtureDelta);

                if (IsDisposing)
                {
                    _logger.InfoFormat("Listener for {0} is disposing - skipping current update", _resource);
                    return;
                }

                // if there was an error from which we haven't recovered yet
                // it might be that with a new sequence, we might recover.
                // So in this case, ignore the update and grab a new 
                // snapshot.
                if (IsErrored)
                {
                    _logger.DebugFormat("Streaming was in an error state for {0} - skipping update and grabbing a new snapshot", _resource);
                    IsErrored = false;
                    RetrieveAndProcessSnapshot();
                    return;
                }



                if (!IsSequenceValid(fixtureDelta))
                {
                    _logger.DebugFormat("Stream update {0} will not be processed because sequence was not valid", fixtureDelta);

                    // if snapshot was already processed with higher sequence no need to process this sequence
                    // THIS should never happen!!
                    if (fixtureDelta.Sequence <= _lastSequenceProcessedInSnapshot)
                    {
                        _logger.WarnFormat("Stream update {0} will be ignored because snapshot with higher sequence={1} was already processed",
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
                        _logger.DebugFormat("{0} has changed matchStatus={1}", _resource, Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus));
                        _platformConnector.ProcessMatchStatus(fixtureDelta);
                    }

                    bool stopStreaming = true;
                    if ((fixtureDelta.IsMatchStatusChanged && fixtureDelta.IsMatchOver) || fixtureDelta.IsDeleted)
                    {

                        if (fixtureDelta.IsDeleted)
                        {
                            ProcessFixtureDelete(fixtureDelta);
                        }
                        else  // Match Over
                        {
                            stopStreaming = ProcessMatchOver(fixtureDelta);
                        }

                        if (stopStreaming)
                            Stop();

                        return;
                    }


                    _logger.InfoFormat("Stream update {0} will not be processed because epoch was not valid", fixtureDelta);

                    SuspendAndReprocessSnapshot(hasEpochChanged);
                    return;
                }

                _logger.InfoFormat("{0} streaming Update was processed successfully", fixtureDelta);
            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), innerException);
                }

                SetErrorState();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), ex);
                SetErrorState();
            }
        }

        internal void ResourceOnStreamDisconnected(object sender, EventArgs eventArgs)
        {
            try
            {
                IsStopping = true;

                // this is for when Dispose() was called
                if (_resource == null)
                    return;

                if (!this.IsFixtureEnded && !IsFixtureDeleted)
                {

                    _logger.WarnFormat("Listener for {0} disconnected from the streaming server, will try reconnect soon", _resource);

                    // do not send a suspend request if we are disposing the StreamListener
                    // (otherwise we send it twice)...note that this should not occure
                    // as the event is removed before calling StopStreaming() 
                    // however, there might be other cases where the disconnect event is raised...
                    if (!IsDisposing)
                        SuspendFixture(SuspensionReason.DISCONNECT_EVENT);
                }
                else
                {
                    _logger.InfoFormat("Listener disconnected for {0} - fixture is over/deleted", _resource);
                }

            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Error while processing OnStreamDisconnected event: {0}", ex);
            }
            finally
            {
                // for when a disconnect event is raised due a failed attempt to connect 
                // (in other words, when we didn't receive a connect event)
                IsConnecting = false;

                IsStreaming = false;
                IsStopping = false;
                IsDisconnected = true;
            }
        }

        internal void ResourceOnStreamConnected(object sender, EventArgs eventArgs)
        {
            try
            {
                IsStreaming = true;
                IsConnecting = false;

                // we are connected, so we don't need the acquire the first snapshot
                // directly
                _isFirstSnapshotProcessed = true;

                _logger.InfoFormat("Listener for {0} is now connected to the streaming server", _resource);

                RetrieveAndProcessSnapshotIfNeeded();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Listener errored when executing OnStreamConnected: {0}", ex);
            }
        }

        private bool IsSequenceValid(Fixture fixtureDelta)
        {
            if (fixtureDelta.Sequence < _currentSequence)
            {
                _logger.DebugFormat("sequence={0} is less than current_sequence={1} in {2}",
                    fixtureDelta.Sequence, _currentSequence, fixtureDelta);
                return false;
            }

            if (fixtureDelta.Sequence - _currentSequence > 1)
            {
                _logger.DebugFormat("sequence={0} is more than one greater that current_sequence={1} in {2} ",
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
                _logger.WarnFormat("Unexpected Epoch={0} when current={1} for {2}", fixtureDelta.Epoch, _currentEpoch, fixtureDelta);
                return false;
            }

            if (fixtureDelta.Epoch == _currentEpoch)
                return true;

            // Cases for fixtureDelta.Epoch > _currentEpoch
            _logger.InfoFormat("Epoch changed for {0} from={1} to={2}", fixtureDelta, _currentEpoch, fixtureDelta.Epoch);

            _currentEpoch = fixtureDelta.Epoch;

            if (fixtureDelta.IsStartTimeChanged)
            {
                _logger.DebugFormat("{0} has had its start time changed", fixtureDelta);
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
            SuspendFixture(SuspensionReason.SUSPENSION);
            RetrieveAndProcessSnapshot(hasEpochChanged);
        }

        private Fixture RetrieveSnapshot(bool setErrorState = true)
        {
            _logger.DebugFormat("Getting Snapshot for {0}", _resource);

            try
            {
                var snapshotJson = _resource.GetSnapshot();

                if (string.IsNullOrEmpty(snapshotJson))
                    throw new Exception(string.Format("Received empty snapshot for {0}", _resource));

                var snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
                if (snapshot != null && string.IsNullOrWhiteSpace(snapshot.Id))
                    throw new Exception(string.Format("Received a snapshot that resulted in an empty snapshot object {0}", _resource));

                _Stats.IncrementValue(AdapterCoreKeys.SNAPSHOT_COUNTER);

                return snapshot;
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An error occured while trying to acquire snapshot for {0}", _resource), e);

                if (setErrorState)
                    SetErrorState();
                else
                    throw;
            }

            return null;
        }

        private void ProcessFirstSnapshotIfNecessary()
        {
            // if we have already processed the first 
            // snapshot directly, don't do it again

            lock (this)
            {
                if (_isFirstSnapshotProcessed)
                {
                    _logger.DebugFormat("Requested to process first snapshot for {0} but it has already been processed - skipping it", _resource);
                    return;
                }

                if (_isProcessingFirstSnapshot)
                {
                    _logger.DebugFormat("Requested to process first snapshot for {0} but it is being processed by another thread", _resource);
                    return;
                }

                _isProcessingFirstSnapshot = true;
            }

            _logger.InfoFormat("Processing first snapshot for {0}", _resource);

            bool tmp = false;
            try
            {
                ProcessSnapshot(RetrieveSnapshot(), true, false);

                //is errored will be set to false in ProcessSnapshot if it's processed succcessfully
                tmp = !IsErrored;
            }
            catch (Exception ex)
            {
                // No need to raise up the exception
                // we will try again later
                _logger.Error(string.Format("Exception occured when trying to process the first snapshot for {0}", _resource), ex);
                SetErrorState();
            }
            finally
            {
                lock (this)
                {
                    _isFirstSnapshotProcessed = tmp;
                    _isProcessingFirstSnapshot = false;
                }
            }
        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false)
        {
            var snapshot = RetrieveSnapshot();
            if (snapshot != null)
                ProcessSnapshot(snapshot, true, hasEpochChanged, !IsErrored);
        }

        private void RetrieveAndProcessSnapshotIfNeeded()
        {

            FixtureState state = _eventState.GetFixtureState(_resource.Id);
            int sequence_number = -1;
            if (state != null)
            {
                sequence_number = state.Sequence;
            }


            int resource_sequence = SequenceOnStreamingAvailable;

            _logger.DebugFormat("{0} has stored sequence={1} and current_sequence={2}", _resource, sequence_number, resource_sequence);

            if (sequence_number == -1 || resource_sequence != sequence_number)
            {
                RetrieveAndProcessSnapshot();
            }
            else
            {
                Fixture fixture = new Fixture { Sequence = sequence_number, Id = _resource.Id };

                if (state != null)
                    fixture.MatchStatus = state.MatchStatus.ToString();

                try
                {
                    //unsuspends markets suspended by adapter
                    _stateManager.StateProvider.SuspensionManager.Unsuspend(fixture.Id);
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
            var logString = isFullSnapshot ? "snapshot" : "stream update";

            if (snapshot == null || (snapshot != null && string.IsNullOrWhiteSpace(snapshot.Id)))
                throw new ArgumentException(string.Format("StreamListener received empty {0} for {1}", logString, _resource));

            _logger.InfoFormat("Processing {0} for {1}", logString, snapshot);

            Stopwatch timer = new Stopwatch();
            timer.Start();

            try
            {
                bool is_inplay = string.Equals(snapshot.MatchStatus, ((int)MatchStatus.InRunning).ToString(), StringComparison.OrdinalIgnoreCase);
                IsInPlay = is_inplay;

                _logger.DebugFormat("Applying market rules for {0}", snapshot);

                _marketsRuleManager.ApplyRules(snapshot);

                if (isFullSnapshot)
                    _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);
                else
                    _platformConnector.ProcessStreamUpdate(snapshot, hasEpochChanged);

                UpdateState(snapshot, isFullSnapshot);

                IsErrored = false;
            }
            catch (FixtureIgnoredException ie)
            {
                _logger.InfoFormat("{0} received a FixtureIgnoredException", _resource);
                _logger.Error("FixtureIgnoredException", ie);
                IsIgnored = true;

                _Stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);
            }
            catch (AggregateException ex)
            {
                _marketsRuleManager.RollbackChanges();

                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("There has been an aggregate error while trying to process {0} {1}", logString, snapshot), innerException);
                }

                _Stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);

                if (setErrorState)
                    SetErrorState();
                else
                    throw;
            }
            catch (Exception e)
            {
                _marketsRuleManager.RollbackChanges();

                _Stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);

                _logger.Error(string.Format("An error occured while trying to process {0} {1}", logString, snapshot), e);

                if (setErrorState)
                    SetErrorState();
                else
                    throw;
            }
            finally
            {
                timer.Stop();
                if (isFullSnapshot)
                    _Stats.AddValue(AdapterCoreKeys.SNAPSHOT_PROCESSING_TIME, timer.ElapsedMilliseconds.ToString());
                else
                    _Stats.AddValue(AdapterCoreKeys.UPDATE_PROCESSING_TIME, timer.ElapsedMilliseconds.ToString());
            }
        }

        private void UpdateState(Fixture snapshot, bool isSnapshot = false)
        {
            _logger.DebugFormat("Updating state for {0} with isSnapshot={1}", snapshot, isSnapshot);

            _marketsRuleManager.CommitChanges();

            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), snapshot.MatchStatus);

            _eventState.UpdateFixtureState(_resource.Sport, _resource.Id, snapshot.Sequence, status);

            if (isSnapshot)
            {
                _lastSequenceProcessedInSnapshot = snapshot.Sequence;
                _currentEpoch = snapshot.Epoch;
            }

            _currentSequence = snapshot.Sequence;
        }

        private void ProcessFixtureDelete(Fixture fixtureDelta)
        {
            IsFixtureDeleted = true;

            _logger.InfoFormat("{0} has been deleted from the GTP Fixture Factory. Suspending all markets and stopping the stream.", _resource);

            try
            {
                SuspendFixture(SuspensionReason.FIXTURE_DELETED);
                _platformConnector.ProcessFixtureDeletion(fixtureDelta);
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An exception occured while trying to process fixture deletion for {0}", _resource), e);
            }


            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus);

            //reset event state
            _marketsRuleManager.OnFixtureUnPublished();
            _eventState.UpdateFixtureState(_resource.Sport, fixtureDelta.Id, -1, status);
        }

        public void ProcessFixtureDelete()
        {
            if (IsFixtureDeleted || IsFixtureEnded)
                return;

            Fixture fixture = new Fixture
            {
                MatchStatus = ((int)MatchStatus.Deleted).ToString(),
                Id = FixtureId
            };

            if (_marketsRuleManager.CurrentState != null)
                fixture.Sequence = _marketsRuleManager.CurrentState.FixtureSequence;

            ProcessFixtureDelete(fixture);
        }

        private bool ProcessMatchOver(Fixture fixtureDelta)
        {
            _logger.InfoFormat("{0} is Match Over. Suspending all markets and stopping the stream.", _resource);

            try
            {
                SuspendAndReprocessSnapshot(true);

                //can't proceed if fixture errored
                if (IsErrored)
                {
                    _logger.WarnFormat("Fixture {0} couldn't retrieve or process the match over snapshot. It will retry shortly.", fixtureDelta);
                    return true;
                }


                if (_settings.StopStreamingDelayMinutes != 0 && _settings.ShouldDelayStopStreaming(Sport))
                {
                    _logger.InfoFormat("Streaming for {0} will be stopped in {1} minutes", _resource, _settings.StopStreamingDelayMinutes);
                    Task.Factory.StartNew(PerformDelayedStop);
                    return false;
                }

                IsFixtureEnded = true;
                _stateManager.ClearState(_resource.Id);
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An error occured while trying to process match over snapshot {0}", fixtureDelta), e);
            }

            return true;
        }

        private void PerformDelayedStop()
        {
            if (_performingDelayedStop)
                return;

            // make sure we perform this only once
            _performingDelayedStop = true;

            Task.Delay(TimeSpan.FromMinutes(_settings.StopStreamingDelayMinutes)).Wait();
            _logger.InfoFormat("Performing delayed stop for {0}", _resource);
            this.IsFixtureEnded = true;
            _stateManager.ClearState(_resource.Id);
            Stop();
        }

        /// <summary>
        /// Dispose the current stream listener
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logger.InfoFormat("Disposing listener for {0}", _resource);
                IsDisposing = true;

                Stop();

                // this is for not sending twice the suspension command
                if (!IsFixtureDeleted && !IsFixtureEnded)
                    SuspendFixture(SuspensionReason.FIXTURE_DISPOSING);

                // free the resource instantiated by the SDK
                _resource = null;

            }
            finally
            {
                IsConnecting = false;
                IsStreaming = false;
                IsDisposing = false;
                _logger.Info("Listener disposed");
            }
        }
    }
}

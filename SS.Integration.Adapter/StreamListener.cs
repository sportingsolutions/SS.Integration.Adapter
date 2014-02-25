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
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.UdapiClient;
using SS.Integration.Adapter.UdapiClient.Model;
using SportingSolutions.Udapi.Sdk.Interfaces;
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

        private readonly string _sportName;

        private readonly IResourceFacade _resource;

        private readonly Fixture _fixtureSnapshot;

        private readonly IAdapterPlugin _platformConnector;

        private readonly IEventState _eventState;

        private int _currentSequence = -1;
        private int _lastSequenceProcessedInSnapshot = -1;
        private int _currentEpoch;
        private bool _isUpdateBeingProcessed = false;

        /// <summary>
        /// true if temporarily lost connection from stream
        /// </summary>
        private bool _hasLostConnection;

        /// <summary>
        /// true if has successfuly established connection to stream server
        /// </summary>
        private bool _isStreamConnected;

        private readonly MarketsFilter _marketsFilter;
        private Task _processingTask;
        private Task _sequenceSynchroniser;
        private IStatsHandle _Stats;


        public StreamListener(string sportName, IResourceFacade resource, Fixture fixtureSnapshot, IAdapterPlugin platformConnector, IEventState eventState, IObjectProvider<IDictionary<string, MarketState>> marketFiltersStorageProvider, int currentSequence = -1)
        {
            _logger.InfoFormat("Instantiating StreamListener for {0} with sequence={1}", fixtureSnapshot, currentSequence);
            if (fixtureSnapshot == null)
            {
                _logger.WarnFormat("Snapshot passed to streamlistener for fixtureId={0} was null, stream listener will not be created!", resource.Id);
                throw new ArgumentException(
                    "Snapshot passed to streamlistener for fixtureId={0} was null, stream listener will not be created!");
            }

            _sportName = sportName;
            _resource = resource;
            _fixtureSnapshot = fixtureSnapshot;
            _platformConnector = platformConnector;
            _eventState = eventState;
            _currentSequence = currentSequence;
            _currentEpoch = fixtureSnapshot.Epoch;

            _hasLostConnection = false;
            _isStreamConnected = false;

            IsFixtureEnded = false;
            IsErrored = false;
            IsIgnored = false;

            // the null check shouldn't be needed but there were parsing errors in the logs so I've added it here as a precaution
            IsFixtureSetup = fixtureSnapshot.MatchStatus != null &&
                (int.Parse(fixtureSnapshot.MatchStatus) == (int)MatchStatus.Setup
                ||
                int.Parse(fixtureSnapshot.MatchStatus) == (int)MatchStatus.Ready);

            _marketsFilter = new MarketsFilter(fixtureSnapshot, marketFiltersStorageProvider);

            _Stats = StatsManager.Instance["StreamListener"].GetHandle(fixtureSnapshot.Id);
            _Stats.SetValue(StreamListenerKeys.FIXTURE, fixtureSnapshot.Id);
            _Stats.SetValue(StreamListenerKeys.STATUS, "Created");
        }

        /// <summary>
        /// If match is over, fixture is ended and listener will not pass any update
        /// </summary>
        public bool IsFixtureEnded { get; private set; }

        /// <summary>
        /// This Listener has errors and needs to be re-created
        /// </summary>
        public bool IsErrored { get; private set; }

        /// <summary>
        /// The fixture is ignored (and listener will not push updates) as there's a problem with target platform
        /// </summary>
        public bool IsIgnored { get; private set; }

        public bool IsFixtureDeleted { get; private set; }

        /// <summary>
        /// If the fixture is Setup the listener would not connect to stream
        /// </summary>
        public bool IsFixtureSetup { get; private set; }

        public bool Start()
        {
            if (!IsErrored)
            {
                _logger.InfoFormat("Starting Listener for sport={0} {1}", _sportName, _resource);
            }
            else
            {
                _logger.InfoFormat("Re-starting Listener for sport={0} {1} because it failed in previous attempt", _sportName, _resource);
            }

            if (_currentSequence > 0)
            {
                _logger.InfoFormat("{0} starts streaming with sequence={1}", _resource, _currentSequence);
            }

            return SetupListener();
        }

        private bool SetupListener()
        {
            if (_resource == null)
            {
                _logger.WarnFormat("Listener for sport={0} cannot listen as resource (fixture) is null", _sportName);
                _Stats.AddMessage(GlobalKeys.CAUSE, "Fixture is null").SetValue(StreamListenerKeys.STATUS, "Error");
                IsErrored = true;
                return false;
            }

            if (_processingTask != null)
            {
                _logger.InfoFormat("Processing task is already created. No action will be taken to refresh it. {0}", _fixtureSnapshot);
                return false;
            }

            try
            {
                _logger.InfoFormat("Processing snapshot for first time for {0}", _resource);

                if (_eventState.GetCurrentSequence(_sportName, _resource.Id) == -1)
                {
                    _platformConnector.ProcessSnapshot(_fixtureSnapshot);
                }
                else if (_currentSequence != _eventState.GetCurrentSequence(_sportName, _resource.Id))
                    _platformConnector.ProcessSnapshot(_fixtureSnapshot);
                else
                {
                    //sequence didn't change
                    _platformConnector.UnSuspend(_fixtureSnapshot);
                    _logger.DebugFormat("Stored sequence={1} skipping processing snapshot for {0}", _fixtureSnapshot, _currentSequence);
                }

                UpdateState(_fixtureSnapshot, true);
                
                IsErrored = false;
            }
            catch (FixtureIgnoredException)
            {
                _logger.WarnFormat("Fixture {0} will be ignored.", _resource.ToString());
                _Stats.SetValue(StreamListenerKeys.STATUS, "Fixture Ignored");
                IsIgnored = true;

                // state is required for fixture deletion
                _eventState.UpdateFixtureState(_resource.Sport, _resource.Id, -1, _resource.MatchStatus);

                return false;
            }
            catch (Exception ex)
            {
                IsErrored = true;
                SuspendMarkets();

                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _logger.Error(string.Format("Error processing snapshot for {0}", _resource.ToString()), ex);

                return false;
            }

            _processingTask = Task.Factory.StartNew(StreamSetup, TaskCreationOptions.LongRunning);
            _processingTask.ContinueWith(
                t =>
                {
                    _logger.ErrorFormat("Error occured while streaming {0} {1}", _resource.Id, t.Exception);
                    IsErrored = true;
                    _Stats.AddMessage(GlobalKeys.CAUSE, t.Exception).SetValue(StreamListenerKeys.STATUS, "Error");
                },
                TaskContinuationOptions.OnlyOnFaulted);

            return true;
        }

        private void StreamSetup()
        {
            _resource.StreamConnected += ResourceOnStreamConnected;
            _resource.StreamDisconnected += ResourceOnStreamDisconnected;
            _resource.StreamEvent += ResourceOnStreamEvent;

            // Only start streaming if fixture is not Setup/Ready
            if (!IsFixtureSetup)
            {
                ConnectToStreamServer();
            }
            else
            {
                _Stats.SetValue(StreamListenerKeys.STATUS, "In Setup");
                _logger.InfoFormat(
                    "{0} is in Setup stage so the listener will not connect to streaming server whilst it's in this stage",
                    _resource);
            }
        }

        private void ConnectToStreamServer()
        {
            try
            {
                _resource.StartStreaming();

                _isStreamConnected = true;
                _Stats.SetValue(StreamListenerKeys.STATUS, "Connected");
            }
            catch (Exception ex)
            {
                IsErrored = true;

                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _logger.Error(string.Format("An error has occured when trying to connect to stream server for {0}", _resource), ex);
            }
        }

        /// <summary>
        /// Connect to Streaming server 
        /// The fixture should not be in Setup/Ready stage when calling this method
        /// </summary>
        public void StartStreaming()
        {
            // Need to get a snapshot as the delta with match status change to PreMatch is lost (due to listener was not connected to stream at that moment in time)
            try
            {
                RetrieveAndProcessSnapshot();
                ConnectToStreamServer();

                IsFixtureSetup = _eventState.GetFixtureState(_resource.Id).MatchStatus == MatchStatus.Setup;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("{0} Error trying to process snapshot when connecting to stream", _resource.ToString()), ex);
                IsErrored = true;
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _marketsFilter.RollbackChanges();
            }
        }

        public void Stop()
        {
            _logger.InfoFormat("Stopping Listener for {0} sport={1}", _resource, _sportName);

            SynchroniseUpdateProcessing();

            if (IsErrored)
                SuspendMarkets();

            if (_resource != null)
            {
                _resource.StreamConnected -= ResourceOnStreamConnected;
                _resource.StreamDisconnected -= ResourceOnStreamDisconnected;
                _resource.StreamEvent -= ResourceOnStreamEvent;

                if (_isStreamConnected)
                {
                    _resource.StopStreaming();
                }
            }

            if (_processingTask != null &&
                    (_processingTask.Status == TaskStatus.RanToCompletion ||
                     _processingTask.Status == TaskStatus.Faulted || _processingTask.Status == TaskStatus.Canceled))
            {
                _processingTask.Dispose();
            }

            if (_sequenceSynchroniser != null)
                _sequenceSynchroniser.Dispose();

            _isStreamConnected = false;
            _processingTask = null;
        }

        public void SuspendMarkets(bool fixtureLevelOnly = true)
        {
            _logger.InfoFormat("Suspending Markets for {0} fixtureLevelOnly={1}", _resource, fixtureLevelOnly);

            try
            {
                _platformConnector.Suspend(_resource.Id);
                if (!fixtureLevelOnly)
                {
                    var suspendedSnapshot = _marketsFilter.GenerateAllMarketsSuspenssion(_resource.Id);
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

        private void SynchroniseUpdateProcessing()
        {
            if (_sequenceSynchroniser == null || _sequenceSynchroniser.IsCompleted)
                return;

            _logger.DebugFormat("Waiting for sequence synchornisation to complete for {0}", _fixtureSnapshot);
            _sequenceSynchroniser.Wait();
            _logger.DebugFormat("Sequence synchornisation is complete for {0}", _fixtureSnapshot);
        }

        
        public void ResourceOnStreamEvent(object sender, StreamEventArgs streamEventArgs)
        {
            try
            {
                SynchroniseUpdateProcessing();
                _isUpdateBeingProcessed = true;
                var deltaMessage = streamEventArgs.Update.FromJson<StreamMessage>();
                var fixtureDelta = deltaMessage.GetContent<Fixture>();

                _logger.InfoFormat("{0} streaming Update arrived", fixtureDelta);

                if (!IsSequenceValid(fixtureDelta))
                {
                    _logger.DebugFormat(
                        "Stream update {0} with sequence={1} will not be processed because sequence was not valid",
                        fixtureDelta, fixtureDelta.Sequence);

                    _Stats.AddMessage(GlobalKeys.CAUSE, "Sequence not valid").SetValue(StreamListenerKeys.LAST_INVALID_SEQUENCE, fixtureDelta.Sequence);
                    _Stats.IncrementValue(StreamListenerKeys.INVALID_SEQUENCE);

                    // if snapshot was already processed with higher sequence no need to process this sequence
                    if (fixtureDelta.Sequence <= _lastSequenceProcessedInSnapshot)
                    {
                        _logger.DebugFormat(
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
                    _marketsFilter.FilterInactiveMarkets(fixtureDelta);
                    _platformConnector.ProcessStreamUpdate(fixtureDelta, hasEpochChanged);

                    _Stats.IncrementValue(StreamListenerKeys.UPDATE_PROCESSED);
                    UpdateState(fixtureDelta);
                }
                else
                {
                    _logger.DebugFormat(
                        "Stream update {0} will not be processed because epoch was not valid",
                        fixtureDelta);

                    _Stats.AddMessage(GlobalKeys.CAUSE, "Epoch was not valid").SetValue(StreamListenerKeys.LAST_INVALID_SEQUENCE, fixtureDelta.Sequence);
                    _Stats.IncrementValue(StreamListenerKeys.INVALID_EPOCH); 
                }

                _logger.InfoFormat("{0} streaming Update was processed successfully", fixtureDelta);
                _Stats.SetValue(StreamListenerKeys.LAST_SEQUENCE, fixtureDelta.Sequence);
            }
            catch (AggregateException ex)
            {
                IsErrored = true;
                _marketsFilter.RollbackChanges();
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("Error processing update that arrived for {0}", _resource),
                                  innerException);
                    _Stats.AddMessage(GlobalKeys.CAUSE, innerException);
                }

                _Stats.SetValue(StreamListenerKeys.STATUS, "Error");
            }
            catch (Exception ex)
            {
                IsErrored = true;
                _Stats.AddMessage(GlobalKeys.CAUSE, ex).SetValue(StreamListenerKeys.STATUS, "Error");
                _marketsFilter.RollbackChanges();
                _logger.Error(string.Format("Error processing update that arrived for {0}", _resource), ex);
            }
            finally
            {
                _isUpdateBeingProcessed = false;
            }
        }

        public void ResourceOnStreamDisconnected(object sender, EventArgs eventArgs)
        {
            if (!this.IsFixtureEnded)
            {
                IsErrored = true;
                _hasLostConnection = true;
                _logger.WarnFormat("Stream disconnected due to problem with {0}, suspending markets, will try reconnect within 1 minute", _resource);
                _Stats.AddMessage(GlobalKeys.CAUSE, "Stream disconnected").SetValue(StreamListenerKeys.STATUS, "Disconnected");

                SuspendMarkets();
            }
            else
            {
                _Stats.AddMessage(GlobalKeys.CAUSE, "Fixture is over").SetValue(StreamListenerKeys.STATUS, "Disconnected");
                _logger.InfoFormat("Stream disconnected for {0}", _resource);
            }
        }

        public void ResourceOnStreamConnected(object sender, EventArgs eventArgs)
        {
            _logger.InfoFormat("Stream connected for {0}", _resource);
            _Stats.IncrementValue(StreamListenerKeys.RESTARTED);

            if (_hasLostConnection)
            {
                _hasLostConnection = false;

                try
                {
                    RetrieveAndProcessSnapshot();
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("{0} Error trying to process snapshot when reconnecting", _resource), ex);
                    _marketsFilter.RollbackChanges();
                }
            }
        }

        /// <summary>
        /// Comparison is done from the perspective of the current sequence. 
        /// </summary>
        private int CompareSequence(int receivedSequence)
        {
            if (receivedSequence < _currentSequence)
                return 1;

            if (receivedSequence - _currentSequence > 1)
                return -1;

            return 0;
        }

        private bool IsSequenceValid(Fixture fixtureDelta)
        {
            var comparisonResult = CompareSequence(fixtureDelta.Sequence);

            if (comparisonResult == 1)
            {
                _logger.InfoFormat("{0} sequence={1} is less than currentSequence={2} in {3}", _resource, fixtureDelta.Sequence, _currentSequence, fixtureDelta);
                return false;
            }
            else if (comparisonResult == -1)
            {
                _logger.WarnFormat("{0} sequence={1} is more than one greater that currentSequence={2} in {3} ", _resource, fixtureDelta.Sequence, _currentSequence, fixtureDelta);
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
                _logger.WarnFormat("Unexpected Epoch {0} when current is {1} for {2}", fixtureDelta.Epoch, _currentEpoch, _resource);
            }
            else if (fixtureDelta.Epoch == _currentEpoch)
            {
                return true;
            }

            // Cases for fixtureDelta.Epoch > _currentEpoch
            _logger.InfoFormat("Epoch changed for {0} from {1} to {2}", _resource, _currentEpoch, fixtureDelta.Epoch);

            _currentEpoch = fixtureDelta.Epoch;

            if (fixtureDelta.IsStartTimeChanged)
            {
                _logger.InfoFormat("{0} has had its start time changed", _resource);
                return true;
            }

            if (fixtureDelta.IsMatchStatusChanged && !string.IsNullOrEmpty(fixtureDelta.MatchStatus))
            {
                _logger.InfoFormat("{0} has changed matchStatus={1}", _resource, Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus));

                _platformConnector.ProcessMatchStatus(fixtureDelta);

            }

            if ((fixtureDelta.IsMatchStatusChanged && fixtureDelta.IsMatchOver) || fixtureDelta.IsDeleted)
            {

                if (fixtureDelta.IsDeleted)
                {
                    FixtureDeleted(fixtureDelta);
                }
                else  // Match Over
                {
                    _logger.InfoFormat("{0} is Match Over. Suspending all markets and stopping the stream.", _resource);

                    SuspendMarkets();
                    this.RetrieveAndProcessSnapshot(hasEpochChanged);
                    _platformConnector.ProcessFixtureDeletion(fixtureDelta);
                    this.IsFixtureEnded = true;
                    _marketsFilter.Clear();
                }

                Stop();

                return false;
            }

            SuspendAndReprocessSnapshot(hasEpochChanged);

            return false;
        }

        private void FixtureDeleted(Fixture fixtureDelta)
        {
            IsFixtureDeleted = true;

            _logger.InfoFormat(
                "{0} has been deleted from the GTP Fixture Factroy. Suspending all markets and stopping the stream.", _resource);

            SuspendMarkets(fixtureLevelOnly: false);

            //reset event state
            _eventState.UpdateFixtureState(_sportName, fixtureDelta.Id, -1, GetMatchStatusFromSnapshot(fixtureDelta));
        }

        private void SuspendAndReprocessSnapshot(bool hasEpochChanged = false)
        {
            Fixture snapshot = null;

            try
            {
                SuspendMarkets();

                snapshot = RetrieveSnapshot();
            }
            catch (AggregateException ex)
            {
                IsErrored = true;
                foreach (var innerException in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("There has been an error when trying to Suspend and Reprocess snapshot for {0}", _resource), innerException);
                }
            }
            catch (Exception ex)
            {
                IsErrored = true;
                _logger.Error(string.Format("There has been an error when trying to Suspend and Reprocess snapshot for {0}", _resource), ex);
            }

            if (snapshot != null)
            {
                _marketsFilter.FilterInactiveMarkets(snapshot);

                _logger.DebugFormat("Sending snapshot {0} with ",snapshot);

                _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);

                UpdateState(snapshot, true);
                
                _logger.DebugFormat("Processed snapshot {0}", snapshot);
            }
        }

        private static MatchStatus GetMatchStatusFromSnapshot(Fixture snapshot)
        {
            return (MatchStatus)Enum.Parse(typeof(MatchStatus), snapshot.MatchStatus);
        }

        private Fixture RetrieveSnapshot()
        {
            _logger.DebugFormat("Getting Snapshot for {0}", _resource);

            var snapshotJson = _resource.GetSnapshot();

            if (string.IsNullOrEmpty(snapshotJson))
            {
                _logger.ErrorFormat("Received empty Snapshot for {0}", _resource);
                return null;
            }

            var snapshot = snapshotJson.FromJson<Fixture>();

            return snapshot;
        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false)
        {
            _logger.DebugFormat("Getting snapshot for {0}", _resource);

            var snapshot = RetrieveSnapshot();

            if (snapshot.IsMatchOver)
                _marketsFilter.VoidUnsettled(snapshot);

            _marketsFilter.FilterInactiveMarkets(snapshot);

            _logger.DebugFormat("Sending snapshot to plugin for {0}", _resource);
            _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);
            
            UpdateState(snapshot, true);
        }

        private void UpdateState(Fixture snapshot, bool isSnapshot = false)
        {
            _marketsFilter.CommitChanges();
            _eventState.UpdateFixtureState(_resource.Sport, _resource.Id, snapshot.Sequence, GetMatchStatusFromSnapshot(snapshot));

            if (isSnapshot)
            {
                _lastSequenceProcessedInSnapshot = snapshot.Sequence;
                _Stats.IncrementValue(StreamListenerKeys.SNAPSHOT_RETRIEVED);
            }

            _currentSequence = snapshot.Sequence;

            _Stats.SetValue(StreamListenerKeys.LAST_SEQUENCE, _currentSequence);
        }

        public bool CheckStreamHealth(int maxPeriodWithoutMessage, int receivedSequence = -1)
        {
            if (IsFixtureSetup || !_isStreamConnected || IsFixtureDeleted)
            {
                // Stream has not yet started as fixture is Setup/Ready
                // Do not remove flag _isStreamConnected from IF as the snapshot may take long to process when fixture becomes PreMatch and before start streaming
                // If removed, this check method will kill the listener and adapter will create another all over again

                return true;
            }

            var shouldProcessSnapshot = (_sequenceSynchroniser == null || _sequenceSynchroniser.IsCompleted)
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
            }

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
                _logger.WarnFormat("Stream for fixtureId={0} has not received a message in {1}, suspending markets, will try to reconnect within 1 minute", _resource.Id, timespan.TotalSeconds);
                SuspendMarkets();
            }

            return !IsErrored;
        }
    }
}

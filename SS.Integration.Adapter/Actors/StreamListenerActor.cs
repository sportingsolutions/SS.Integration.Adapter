//Copyright 2017 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Exceptions;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Extensions;
using System;
using System.Linq;
using SportingSolutions.Udapi.Sdk;
using SportingSolutions.Udapi.Sdk.Extensions;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Exceptions;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class is responsible for managing resource and streaming 
    /// </summary>
    public class StreamListenerActor : ReceiveActor, IWithUnboundedStash
    {
        #region Constants

        public const string ActorName = nameof(StreamListenerActor);

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActor).ToString());
        private readonly ISettings _settings;
        private readonly IStreamHealthCheckValidation _streamHealthCheckValidation;
        private readonly IFixtureValidation _fixtureValidation;
        private readonly IResourceFacade _resource;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IStateManager _stateManager;
        private readonly ISuspensionManager _suspensionManager;
        private readonly IMarketRulesManager _marketsRuleManager;
        private readonly IActorRef _resourceActor;
        private readonly IActorRef _streamHealthCheckActor;
        private readonly IActorRef _streamStatsActor;

        private readonly string _fixtureId;
        private int _currentEpoch;
        private int _currentSequence;
        private int _lastSequenceProcessedInSnapshot;
        private DateTime? _fixtureStartTime;
        private bool _fixtureIsSuspended;
        private Exception _erroredException;
        //this field helps track the Stream Listener Actor Initialization 
        //so it can notify the Stream Listener Manager when actor creation failed
        private bool _isInitializing;

        #endregion

        #region Properties

        internal StreamListenerState State { get; private set; }

        public IStash Stash { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="platformConnector"></param>
        /// <param name="resource"></param>
        /// <param name="stateManager"></param>
        /// <param name="suspensionManager"></param>
        /// <param name="streamHealthCheckValidation"></param>
        /// <param name="fixtureValidation"></param>
        public StreamListenerActor(
            ISettings settings,
            IAdapterPlugin platformConnector,
            IResourceFacade resource,
            IStateManager stateManager,
            ISuspensionManager suspensionManager,
            IStreamHealthCheckValidation streamHealthCheckValidation,
            IFixtureValidation fixtureValidation)
        {
            try
            {
                _isInitializing = true;

                _settings = settings ?? throw new ArgumentNullException(nameof(settings));
                _platformConnector = platformConnector ?? throw new ArgumentNullException(nameof(platformConnector));
                _resource = resource ?? throw new ArgumentNullException(nameof(resource));
                _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
                _suspensionManager = suspensionManager ?? throw new ArgumentNullException(nameof(suspensionManager));
                _marketsRuleManager = _stateManager.CreateNewMarketRuleManager(resource.Id);
                _streamHealthCheckValidation = streamHealthCheckValidation ?? throw new ArgumentNullException(nameof(streamHealthCheckValidation));
                _fixtureValidation = fixtureValidation ?? throw new ArgumentNullException(nameof(fixtureValidation));
                _fixtureId = _resource.Id;
                _resourceActor = Context.ActorOf(
                    Props.Create(() => new ResourceActor(Self, _resource)),
                    ResourceActor.ActorName);
                _streamHealthCheckActor = Context.ActorOf(
                    Props.Create(() => new StreamHealthCheckActor(_resource, _settings, _streamHealthCheckValidation)),
                    StreamHealthCheckActor.ActorName);
                _streamStatsActor = Context.ActorOf(
                    Props.Create(() => new StreamStatsActor()),
                    StreamStatsActor.ActorName);

                Context.Parent.Tell(new NewStreamListenerActorMsg { FixtureId = _resource.Id, Sport = _resource.Sport });

                Initialize();
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Stream Listener instantiation failed for {_resource} - exception - {ex}");
                _erroredException = ex;
                Become(Errored);
            }
        }

        #endregion

        #region Behaviors

        //Initialised but not streaming yet - this can happen when you start fixture in Setup
        private void Initialized()
        {
            State = StreamListenerState.Initialized;

            _logger.Info($"Stream listener for {_resource} moved to Initialized State");

            OnStateChanged();

            Receive<ConnectToStreamServerMsg>(a => ConnectToStreamServer());
            Receive<SuspendAndReprocessSnapshotMsg>(a => SuspendAndReprocessSnapshot());
            Receive<StreamConnectedMsg>(a => Become(Streaming));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
            Receive<StopStreamingMsg>(a => StopStreaming());
            Receive<StreamHealthCheckMsg>(a => StreamHealthCheckMsgHandler(a));
            Receive<RetrieveAndProcessSnapshotMsg>(a => RetrieveAndProcessSnapshot(false, true));
            Receive<ClearFixtureStateMsg>(a => ClearState(true));
            Receive<GetStreamListenerActorStateMsg>(a => Sender.Tell(State));
            Receive<SuspendMessage>(a => Suspend());
            
            try
            {
                RetrieveAndProcessSnapshot();
                Context.Parent.Tell(new StreamListenerInitializedMsg { Resource = _resource });
                _isInitializing = false;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Stream Listener for {_resource} failed on Initialized State when RetrieveAndProcessSnapshot - exception - {ex}");
                _erroredException = ex;
                Become(Errored);
            }
        }

        //Connected and streaming state - all messages should be processed
        private void Streaming()
        {
            State = StreamListenerState.Streaming;

            _logger.Info($"Stream listener for {_resource} moved to Streaming State");

            OnStateChanged();

            Receive<SuspendAndReprocessSnapshotMsg>(a => SuspendAndReprocessSnapshot());
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
            Receive<StopStreamingMsg>(a => StopStreaming());
            Receive<StreamUpdateMsg>(a => StreamUpdateHandler(a));
            Receive<StreamHealthCheckMsg>(a => StreamHealthCheckMsgHandler(a));
            Receive<RetrieveAndProcessSnapshotMsg>(a => RetrieveAndProcessSnapshot(false, true));
            Receive<ClearFixtureStateMsg>(a => ClearState(true));
            Receive<GetStreamListenerActorStateMsg>(a => Sender.Tell(State));
            Receive<SuspendMessage>(a => Suspend());

            try
            {
                var streamConnectedMsg =
                    new StreamConnectedMsg
                    {
                        FixtureId = _fixtureId,
                        FixtureStatus = _resource.MatchStatus.ToString()
                    };
                _streamHealthCheckActor.Tell(streamConnectedMsg);

                var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
                var fixtureState =
                    fixtureStateActor
                        .Ask<FixtureState>(
                            new GetFixtureStateMsg { FixtureId = _fixtureId },
                            TimeSpan.FromSeconds(10))
                        .Result;

                if (_fixtureValidation.IsSnapshotNeeded(_resource, fixtureState))
                {
                    RetrieveAndProcessSnapshot();
                }
                else
                {
                    UnsuspendFixture(fixtureState);
                }

                Stash.UnstashAll();

                Context.Parent.Tell(streamConnectedMsg);
                _isInitializing = false;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Failed moving to Streaming State for {_resource} - exception - {ex}");
                _erroredException = ex;
                Become(Errored);
            }
        }

        //Resource has been disconnected, quick reconnection will occur soon
        private void Disconnected()
        {
            State = StreamListenerState.Disconnected;

            _logger.Info($"Stream listener for {_resource} moved to Disconnected State");

            OnStateChanged();

            Receive<GetStreamListenerActorStateMsg>(a => Sender.Tell(State));

            var streamDisconnectedMessage = new StreamDisconnectedMsg { FixtureId = _fixtureId };

            //tell Stream Stats actor that we got disconnected so it can monitor number of disconnections
            _streamStatsActor.Tell(streamDisconnectedMessage);

            //tell Stream Listener Manager Actor that we got disconnected so it can kill and recreate this child actor
            Context.Parent.Tell(streamDisconnectedMessage);
        }

        //Error has occured, resource will try to recover by processing full snapshot
        private void Errored()
        {
            var prevState = State;
            State = StreamListenerState.Errored;

            _logger.Info($"Stream listener for {_resource} moved to Errored State");

            OnStateChanged();

            SuspendFixture(SuspensionReason.SUSPENSION);
            Exception erroredEx;
            RecoverFromErroredState(prevState, out erroredEx);

            if (erroredEx != null)
            {
                try
                {
                    _logger.Error(
                        $"Suspending Fixture {_resource} with FIXTURE_ERRORED as Stream Listener failed to recover from Errored State - exception - {erroredEx}");
                    SuspendFixture(SuspensionReason.FIXTURE_ERRORED);
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        $"Failed Suspending Fixture {_resource} on Errored State - exception - {ex}");
                }
                finally
                {
                    if (_isInitializing)
                    {
                        Context.Parent.Tell(
                            new StreamListenerCreationFailedMsg
                            {
                                FixtureId = _fixtureId,
                                FixtureStatus = _resource.MatchStatus.ToString(),
                                Exception = erroredEx
                            });
                        _isInitializing = false;
                    }
                }

                if (prevState == StreamListenerState.Initializing)
                {
                    Become(Stopped);
                }
            }

            Receive<SuspendAndReprocessSnapshotMsg>(a => SuspendAndReprocessSnapshot());
            Receive<StopStreamingMsg>(a => StopStreaming());
            Receive<StreamUpdateMsg>(a => RecoverFromErroredState(prevState, out erroredEx));
            Receive<StreamHealthCheckMsg>(a => StreamHealthCheckMsgHandler(a));
            Receive<RetrieveAndProcessSnapshotMsg>(a => RetrieveAndProcessSnapshot(false, true));
            Receive<ClearFixtureStateMsg>(a => ClearState(true));
            Receive<GetStreamListenerActorStateMsg>(a => Sender.Tell(State));
            Receive<SuspendMessage>(a => Suspend());
        }

        //No further messages should be accepted, resource has stopped streaming
        private void Stopped()
        {
            State = StreamListenerState.Stopped;

            _logger.Info($"Stream listener for {_resource} moved to Stopped State");

            OnStateChanged();

            Receive<GetStreamListenerActorStateMsg>(a => Sender.Tell(State));

            //tell Stream Listener Manager Actor that we stopped so it can kill this child actor
            Context.Parent.Tell(new StreamListenerStoppedMsg { FixtureId = _fixtureId });
        }

        #endregion

        #region Message Handlers

        private void StreamHealthCheckMsgHandler(StreamHealthCheckMsg msg)
        {
            msg.StreamingState = State;
            msg.CurrentSequence = _currentSequence;

            _logger.Info(
                $"{_resource} Stream health check message arrived - State = {State}; CurrentSequence = {_currentSequence}");

            _streamHealthCheckActor.Tell(msg);
        }

        private void StreamUpdateHandler(StreamUpdateMsg msg)
        {
            var callTime = DateTime.UtcNow;
            Fixture fixtureDelta = null;

            try
            {
                var streamMessage = msg.Data.FromJson<StreamMessage>();
                fixtureDelta = FixtureHelper.GetFixtureDelta(streamMessage);

                _logger.Info($"{fixtureDelta} stream update arrived");

                if (!_fixtureValidation.IsSequenceValid(fixtureDelta, _currentSequence))
                {
                    _logger.Warn($"Update for {fixtureDelta} will not be processed because sequence is not valid");

                    // if snapshot was already processed with higher sequence no need to process this sequence
                    // THIS should never happen!!
                    if (fixtureDelta.Sequence <= _lastSequenceProcessedInSnapshot)
                    {
                        _logger.Warn(
                            $"Stream update {fixtureDelta} will be ignored because snapshot with higher sequence={_lastSequenceProcessedInSnapshot} was already processed");

                        return;
                    }

                    SuspendAndReprocessSnapshot();
                    return;
                }

                bool hasEpochChanged = fixtureDelta.Epoch != _currentEpoch;

                if (_fixtureValidation.IsEpochValid(fixtureDelta, _currentEpoch))
                {
                    ProcessSnapshot(fixtureDelta, false, hasEpochChanged);
                    _logger.Info($"Update for {fixtureDelta} processed successfully");
                }
                else
                {
                    ProcessInvalidEpoch(fixtureDelta, hasEpochChanged);
                }

                _currentSequence = fixtureDelta.Sequence;
                _currentEpoch = fixtureDelta.Epoch;
            }
            catch (AggregateException ex)
            {
                int total = ex.InnerExceptions.Count;
                int count = 0;
                foreach (var innerEx in ex.InnerExceptions)
                {
                    _logger.Error($"Error processing update for {_resource} {innerEx} ({++count}/{total})");
                }

                _erroredException = ex;
                Become(Errored);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing update {_resource} - exception - {ex}");

                _erroredException = ex;
                Become(Errored);
            }
            finally
            {
                _logger.Debug($"method=StreamUpdateHandler executionTimeInSeconds={(DateTime.UtcNow-callTime).TotalSeconds}  for {fixtureDelta}");
            }
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            try
            {
                _logger.Warn($"Stream got disconnected for {_resource}");

                var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
                var fixtureState =
                    fixtureStateActor
                        .Ask<FixtureState>(
                            new GetFixtureStateMsg { FixtureId = _fixtureId },
                            TimeSpan.FromSeconds(10))
                        .Result;

                if (_streamHealthCheckValidation.ShouldSuspendOnDisconnection(fixtureState, _fixtureStartTime))
                {
                    SuspendFixture(SuspensionReason.DISCONNECT_EVENT);
                }

                Become(Disconnected);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing disconnection for {_resource} - exception - {ex}");

                _erroredException = ex;
                Become(Errored);
            }
        }

        #endregion

        #region Static methods

        public static string GetName(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                throw new ArgumentNullException(nameof(resourceId));

            return string.Concat(ActorName, "-for-", resourceId);
        }

        public static string GetPath(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                throw new ArgumentNullException(nameof(resourceId));

            return string.Concat("/user/", GetName(resourceId));
        }

        #endregion

        #region Protected methods

        protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        #endregion

        #region Private methods

        private void Initialize()
        {
            _logger.Info($"Initializing stream listener for {_resource}");

            try
            {
                State = StreamListenerState.Initializing;

                Receive<StreamConnectedMsg>(a => Become(Streaming));
                Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
                Receive<StreamUpdateMsg>(a => Stash.Stash());
                Receive<RetrieveAndProcessSnapshotMsg>(a => RetrieveAndProcessSnapshot(false, true));
                Receive<ClearFixtureStateMsg>(a => ClearState(true));
                Receive<GetStreamListenerActorStateMsg>(a => Sender.Tell(State));

                var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
                var fixtureState =
                    fixtureStateActor
                        .Ask<FixtureState>(
                            new GetFixtureStateMsg { FixtureId = _fixtureId },
                            TimeSpan.FromSeconds(10))
                        .Result;

                _currentEpoch = fixtureState?.Epoch ?? -1;
                _currentSequence = _resource.Content.Sequence;
                _lastSequenceProcessedInSnapshot = -1;
                _fixtureIsSuspended = false;

                if (!string.IsNullOrEmpty(_resource.Content?.StartTime))
                {
                    _fixtureStartTime = DateTime.Parse(_resource.Content.StartTime);
                }

                if (_resource.IsMatchOver)
                {
                    if (fixtureState != null && fixtureState.MatchStatus != MatchStatus.MatchOver)
                    {
                        ProcessMatchOver();
                    }

                    _logger.Warn($"Stopping actor for {_resource} as the resource is marked as ended");

                    Context.Parent.Tell(
                        new StreamListenerCreationCancelledMsg
                        {
                            FixtureId = _resource.Id,
                            FixtureStatus = _resource.MatchStatus.ToString(),
                            Reason = "Match is over"
                        });

                    Become(Stopped);
                }
                else
                {
                    //either connect to stream server and go to Streaming State, or go to Initialized State
                    if (_streamHealthCheckValidation.CanConnectToStreamServer(_resource, State))
                    {
                        ConnectToStreamServer();
                    }
                    else
                    {
                        Become(Initialized);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error on Initialize resource {_resource} - exception - {ex}");

                _erroredException = ex;
                Become(Errored);
            }
        }

        private void ConnectToStreamServer()
        {
            _logger.Debug($"Starting streaming for {_resource} - resource has sequence={_resource.Content.Sequence}");

            _streamHealthCheckActor.Tell(new ConnectToStreamServerMsg());
            _resourceActor.Tell(new StartStreamingMsg());

            _logger.Debug($"Started streaming for {_resource} - resource has sequence={_resource.Content.Sequence}");
        }

        private bool VerifySequenceOnSnapshot(Fixture snapshot)
        {
            if (snapshot.Sequence < _lastSequenceProcessedInSnapshot)
            {
                _logger.Warn(
                    $"Newer snapshot {snapshot} was already processed on another thread, current sequence={_currentSequence}");
                return false;
            }

            return true;
        }

        private void UnsuspendFixture(FixtureState state)
        {
            Fixture fixture = new Fixture
            {
                Id = _fixtureId,
                Sequence = -1
            };

            if (state != null)
            {
                fixture.Sequence = state.Sequence;
                fixture.MatchStatus = state.MatchStatus.ToString();
            }

            try
            {
                _suspensionManager.Unsuspend(fixture);
                _fixtureIsSuspended = false;
            }
            catch (PluginException ex)
            {
                UpdateStatsError(ex);
                throw;
            }
        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false, bool skipMarketRules = false)
        {
            var snapshot = RetrieveSnapshot();
            var shouldSkipProcessingMarketRules = skipMarketRules || _settings.SkipRulesOnError && _erroredException != null;
            ProcessSnapshot(snapshot, true, hasEpochChanged, shouldSkipProcessingMarketRules);
        }

        private Fixture RetrieveSnapshot()
        {
            _logger.Debug($"Getting snapshot for {_resource}");

            string snapshotJson = null;

            try
            {
                snapshotJson = _resource.GetSnapshot();
            }
            catch (Exception ex)
            {
                var apiError = new ApiException($"GetSnapshot for {_resource} failed", ex);
                UpdateStatsError(apiError);
                throw apiError;
            }

            if (string.IsNullOrEmpty(snapshotJson))
                throw new Exception($"Received empty snapshot for {_resource}");

            var snapshot = FixtureHelper.GetFromJson(snapshotJson);
            if (snapshot == null || snapshot != null && snapshot.Id.IsNullOrWhiteSpace())
                throw new Exception($"Received a snapshot that resulted in an empty snapshot object {_resource}"
                                    + Environment.NewLine +
                                    $"Platform raw data=\"{snapshotJson}\"");

            if (snapshot.Sequence < _currentSequence)
                throw new Exception(
                    $"Received snapshot {snapshot} with sequence lower than currentSequence={_currentSequence}");

            _fixtureStartTime = snapshot.StartTime;

            return snapshot;
        }

        private void ProcessSnapshot(Fixture snapshot, bool isFullSnapshot, bool hasEpochChanged, bool skipMarketRules = false)
        {
            var logString = isFullSnapshot ? "snapshot" : "stream update";

            if (snapshot == null || (snapshot != null && string.IsNullOrWhiteSpace(snapshot.Id)))
                throw new ArgumentException($"Received empty {logString} for {_resource}");

            _logger.Info($"Processing {logString} for {snapshot}");

            try
            {
                if (isFullSnapshot && !VerifySequenceOnSnapshot(snapshot))
                    return;

                _streamStatsActor.Tell(new UpdateStatsStartMsg
                {
                    Fixture = snapshot,
                    Sequence = snapshot.Sequence,
                    IsSnapshot = isFullSnapshot,
                    UpdateReceivedAt = DateTime.UtcNow
                });

                _logger.Info($"BeforeMarketRules MarketsCount={snapshot.Markets.Count} ActiveMarketsCount={snapshot.Markets.Count(_ => _.IsActive)} SelectionsCount={snapshot.Markets.SelectMany(_ => _.Selections).Count()}  {snapshot}");
                if (!skipMarketRules)
                {
                    _marketsRuleManager.ApplyRules(snapshot);
                    snapshot.IsModified = true;
                }
                else
                {
                    _marketsRuleManager.ApplyRules(snapshot, true);
                }
                _logger.Info($"AfterMarketRules MarketsCount={snapshot.Markets.Count} ActiveMarketsCount={snapshot.Markets.Count(_ => _.IsActive)} SelectionsCount={snapshot.Markets.SelectMany(_ => _.Selections).Count()}  {snapshot}");

                if (isFullSnapshot)
                {
                    try
                    {
                        _streamStatsActor.Tell(new UpdatePluginStatsStartMsg()
                        {
                            Fixture = snapshot,
                            Sequence = snapshot.Sequence,
                            IsSnapshot = true,
                            UpdateReceivedAt = DateTime.UtcNow,
                            PluginMethod = "ProcessSnapshot"
                        });
                        _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);
                        _streamStatsActor.Tell(new UpdatePluginStatsFinishMsg
                        {
                            CompletedAt = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        var pluginError = new PluginException($"Plugin ProcessSnapshot {snapshot} error occured", ex);
                        UpdateStatsError(pluginError);
                        throw pluginError;
                    }
                }
                else
                {
                    try
                    {
                        _streamStatsActor.Tell(new UpdatePluginStatsStartMsg()
                        {
                            Fixture = snapshot,
                            Sequence = snapshot.Sequence,
                            IsSnapshot = false,
                            UpdateReceivedAt = DateTime.UtcNow,
                            PluginMethod = "ProcessStreamUpdate"
                        });
                        _platformConnector.ProcessStreamUpdate(snapshot, hasEpochChanged);
                        _streamStatsActor.Tell(new UpdatePluginStatsFinishMsg
                        {
                            CompletedAt = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        var pluginError = new PluginException($"Plugin ProcessStreamUpdate {snapshot} error occured", ex);
                        UpdateStatsError(pluginError);
                        throw pluginError;
                    }
                }


                UpdateFixtureState(snapshot, isFullSnapshot);
                UpdateSupervisorState(snapshot, isFullSnapshot);

                _streamStatsActor.Tell(new UpdateStatsFinishMsg
                {
                    CompletedAt = DateTime.UtcNow
                });
            }
            catch (FixtureIgnoredException)
            {
                _logger.Warn($"{_resource} received a FixtureIgnoredException");
            }
            catch (AggregateException ex)
            {
                _marketsRuleManager.RollbackChanges();

                int total = ex.InnerExceptions.Count;
                int count = 0;
                foreach (var e in ex.InnerExceptions)
                {
                    _logger.Error($"Error processing {logString} for {snapshot} ({++count}/{total})", e);
                }
                throw;
            }
            catch (Exception ex)
            {
                _marketsRuleManager.RollbackChanges();

                _logger.Error($"Error processing {logString} {snapshot}", ex);
                throw;
            }

            _logger.Info($"Finished processing {logString} for {snapshot}");
        }

        private void UpdateSupervisorState(Fixture snapshot, bool isFullSnapshot)
        {
            ActorSelection supervisorActor = Context.System.ActorSelection("/user/SupervisorActor");
            MatchStatus matchStatus;
            supervisorActor.Tell(new UpdateSupervisorStateMsg
            {
                FixtureId = snapshot.Id,
                Sport = _resource.Sport,
                Epoch = snapshot.Epoch,
                CurrentSequence = snapshot.Sequence,
                StartTime = snapshot.StartTime,
                IsSnapshot = isFullSnapshot,
                MatchStatus = Enum.TryParse(snapshot.MatchStatus, out matchStatus)
                    ? (MatchStatus?)matchStatus
                    : null,
                Name = snapshot.FixtureName,
                CompetitionId = snapshot.Tags.ContainsKey("SSLNCompetitionId")
                    ? snapshot.Tags["SSLNCompetitionId"].ToString()
                    : null,
                CompetitionName = snapshot.Tags.ContainsKey("SSLNCompetitionName")
                    ? snapshot.Tags["SSLNCompetitionName"].ToString()
                    : null,
                LastEpochChangeReason = snapshot.LastEpochChangeReason,
                IsStreaming = State == StreamListenerState.Streaming,
                IsSuspended = _fixtureIsSuspended,
                IsErrored = State == StreamListenerState.Errored,
                Exception = _erroredException
            });
        }

        private void ProcessInvalidEpoch(Fixture fixtureDelta, bool hasEpochChanged)
        {
            _fixtureStartTime = fixtureDelta.StartTime ?? _fixtureStartTime;

            if (fixtureDelta.IsDeleted)
            {
                ProcessFixtureDelete(fixtureDelta);
                UpdateSupervisorState(fixtureDelta, false);
                StopStreaming();
                return;
            }

            if (fixtureDelta.IsMatchStatusChanged)
            {
                if (!string.IsNullOrEmpty(fixtureDelta.MatchStatus))
                {
                    _logger.Debug(
                        $"{_resource} has changed matchStatus={Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus)}");

                    try
                    {
                        _streamStatsActor.Tell(new UpdatePluginStatsStartMsg()
                        {
                            Fixture = fixtureDelta,
                            Sequence = fixtureDelta.Sequence,
                            IsSnapshot = false,
                            UpdateReceivedAt = DateTime.UtcNow,
                            PluginMethod = "ProcessMatchStatus"
                        });
                        _platformConnector.ProcessMatchStatus(fixtureDelta);
                        _streamStatsActor.Tell(new UpdatePluginStatsFinishMsg
                        {
                            CompletedAt = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        var pluginError = new PluginException($"Plugin ProcessMatchStatus {fixtureDelta} error occured", ex);
                        UpdateStatsError(pluginError);
                        throw pluginError;
                    }
                }

                if (fixtureDelta.IsMatchOver)
                {
                    ProcessMatchOver();
                    StopStreaming();
                    return;
                }
            }

            //epoch change reason - aggregates LastEpochChange reasons into string like "BaseVariables,Starttime"
            var reason =
                fixtureDelta.LastEpochChangeReason != null && fixtureDelta.LastEpochChangeReason.Length > 0
                    ? fixtureDelta.LastEpochChangeReason.Select(x => ((EpochChangeReason)x).ToString())
                        .Aggregate((first, second) => $"{first}, {second}")
                    : "Unknown";
            _logger.Info(
                $"Stream update {fixtureDelta} has epoch change with reason {reason}, the snapshot will be processed instead.");

            SuspendAndReprocessSnapshot(hasEpochChanged);
        }

        private void ProcessFixtureDelete(Fixture fixtureDelta)
        {
            _logger.Info(
                $"{_resource} has been deleted from the GTP Fixture Factory. Suspending all markets and stopping the stream.");

            Fixture fixtureDeleted = new Fixture
            {
                Id = _fixtureId,
                FixtureName = fixtureDelta.FixtureName,
                MatchStatus = ((int)MatchStatus.Deleted).ToString()
            };

            if (_marketsRuleManager.CurrentState != null)
                fixtureDeleted.Sequence = _marketsRuleManager.CurrentState.FixtureSequence;

            try
            {
                SuspendFixture(SuspensionReason.FIXTURE_DELETED);
                try
                {
                    _streamStatsActor.Tell(new UpdatePluginStatsStartMsg()
                    {
                        Fixture = fixtureDelta,
                        Sequence = fixtureDelta.Sequence,
                        IsSnapshot = false,
                        UpdateReceivedAt = DateTime.UtcNow,
                        PluginMethod = "ProcessFixtureDeletion"
                    });

                    _platformConnector.ProcessFixtureDeletion(fixtureDeleted);
                    _streamStatsActor.Tell(new UpdatePluginStatsFinishMsg
                    {
                        CompletedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    var pluginError = new PluginException($"Plugin ProcessFixtureDeletion {fixtureDeleted} error occured", ex);
                    UpdateStatsError(pluginError);
                    throw pluginError;
                }
            }
            catch (Exception e)
            {
                _logger.Error($"An exception occured while trying to process fixture deletion for {_resource}", e);
                throw;
            }

            //reset fixture state
            _marketsRuleManager.OnFixtureUnPublished();
            var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
            var updateFixtureStateMsg = new UpdateFixtureStateMsg
            {
                FixtureId = _fixtureId,
                Sport = _resource.Sport,
                Status = MatchStatus.Deleted,
                Sequence = -1,
                Epoch = _currentEpoch
            };
            fixtureStateActor.Tell(updateFixtureStateMsg);
        }

        private void ProcessMatchOver()
        {
            _logger.Info($"{_resource} is Match Over. Suspending all markets and stopping the stream.");

            try
            {
                SuspendAndReprocessSnapshot(true);
            }
            catch (Exception ex)
            {
                _logger.Error($"An error occured while trying to process match over resource {_resource} - exception - {ex}");
                throw;
            }
        }

        private void ClearState(bool stopStreaming = false)
        {
            if (stopStreaming)
            {
                StopStreaming();
            }

            _stateManager.ClearState(_fixtureId);
            var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
            fixtureStateActor.Tell(new RemoveFixtureStateMsg { FixtureId = _fixtureId });
        }

        private void SuspendAndReprocessSnapshot(bool hasEpochChanged = false)
        {
            SuspendFixture(SuspensionReason.SUSPENSION);
            RetrieveAndProcessSnapshot(hasEpochChanged);
        }

        private void Suspend()
        {
            SuspendFixture(SuspensionReason.SUSPENSION);
        }

        private void SuspendFixture(SuspensionReason reason)
        {
            _logger.Info($"Suspending fixtureId={_resource} due reason={reason}");

            try
            {
                _suspensionManager.Suspend(new Fixture { Id = _fixtureId }, reason);
                _fixtureIsSuspended = true;
            }
            catch (PluginException ex)
            {
                UpdateStatsError(ex);
                throw;
            }
        }

        private void UpdateFixtureState(Fixture snapshot, bool isSnapshot = false)
        {
            _marketsRuleManager.CommitChanges();

            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), snapshot.MatchStatus);

            var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
            var updateFixtureStateMsg = new UpdateFixtureStateMsg
            {
                FixtureId = _fixtureId,
                Sport = _resource.Sport,
                Status = status,
                Sequence = snapshot.Sequence,
                Epoch = snapshot.Epoch
            };
            fixtureStateActor.Tell(updateFixtureStateMsg);

            if (isSnapshot)
            {
                _lastSequenceProcessedInSnapshot = snapshot.Sequence;
            }

            _currentSequence = snapshot.Sequence;
            _currentEpoch = snapshot.Epoch;
        }

        private void StopStreaming()
        {
            _resourceActor.Tell(new StopStreamingMsg());

            Become(Stopped);
        }

        private void RecoverFromErroredState(StreamListenerState prevState, out Exception erroredEx)
        {
            erroredEx = null;

            try
            {
                _logger.Warn(
                    $"Fixture {_resource} is in Errored State - trying now to reprocess full snapshot");

                RetrieveAndProcessSnapshot();

                switch (prevState)
                {
                    case StreamListenerState.Initializing:
                        {
                            Initialize();
                            break;
                        }
                    case StreamListenerState.Streaming:
                        {
                            Become(Streaming);
                            break;
                        }
                }

                _erroredException = null;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Fixture {_resource} failed to recover from Errored State - exception - {ex}");

                erroredEx = ex;
            }
        }

        private void UpdateStatsError(Exception ex)
        {
            _streamStatsActor.Tell(new UpdateStatsErrorMsg
            {
                ErrorOccuredAt = DateTime.UtcNow,
                Error = ex
            });
        }

        private void OnStateChanged()
        {
            Context.Parent.Tell(new StreamListenerActorStateChangedMsg
            {
                FixtureId = _resource.Id,
                Sport = _resource.Sport,
                NewState = State
            });
        }

        #endregion
    }
}

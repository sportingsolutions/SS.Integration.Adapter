using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Exceptions;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Extensions;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;
using System;
using System.Diagnostics;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Extensions;
using SS.Integration.Adapter.Actors.Messages;

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

        #region Enums

        internal enum StreamListenerState
        {
            Initializing,
            Initialized,
            Streaming,
            Disconnected,
            Errored,
            Stopped
        }

        #endregion

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActor).ToString());
        private readonly ISettings _settings;
        private readonly IResourceFacade _resource;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IEventState _eventState;
        private readonly IStatsHandle _stats;
        private readonly IStateManager _stateManager;
        private readonly IMarketRulesManager _marketsRuleManager;
        private readonly IActorRef _resourceActor;
        private readonly IActorRef _streamHealthCheckActor;

        private readonly string _fixtureId;
        private int _currentEpoch;
        private int _currentSequence;
        private int _lastSequenceProcessedInSnapshot;
        private DateTime? _fixtureStartTime;

        #endregion

        #region Properties

        internal StreamListenerState State { get; private set; }

        public IStash Stash { get; set; }

        #endregion

        #region Constructors

        public StreamListenerActor(
            IResourceFacade resource,
            IAdapterPlugin platformConnector,
            IEventState eventState,
            IStateManager stateManager,
            ISettings settings)
        {
            try
            {
                _resource = resource ?? throw new ArgumentNullException(nameof(resource));
                _platformConnector = platformConnector ?? throw new ArgumentNullException(nameof(platformConnector));
                _eventState = eventState ?? throw new ArgumentNullException(nameof(eventState));
                _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
                _marketsRuleManager = _stateManager.CreateNewMarketRuleManager(resource.Id);
                _stats = StatsManager.Instance[string.Concat("adapter.core.sport.", resource.Sport)].GetHandle();
                _settings = settings ?? throw new ArgumentNullException(nameof(settings));
                _fixtureId = _resource.Id;
                _resourceActor = Context.ActorOf(
                    Props.Create(() => new ResourceActor(_resource)),
                    ResourceActor.ActorName);
                _streamHealthCheckActor = Context.ActorOf(
                    Props.Create(() => new StreamHealthCheckActor(_resource, _settings, Context)),
                    StreamHealthCheckActor.ActorName);

                Initialize();
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Stream Listener instantiation failed for resource {_resource} - exception - {ex}");
                Become(Errored);
            }
        }

        #endregion

        #region Behaviors

        //Initialised but not streaming yet - this can happen when you start fixture in Setup
        private void Initialized()
        {
            State = StreamListenerState.Initialized;

            Receive<StreamConnectedMsg>(a => Become(Streaming));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
            Receive<ResourceStateUpdateMsg>(a => ResourceStateUpdateMsgHandler(a));
        }

        //Connected and streaming state - all messages should be processed
        private void Streaming()
        {
            State = StreamListenerState.Streaming;

            try
            {
                Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
                Receive<ResourceStateUpdateMsg>(a => ResourceStateUpdateMsgHandler(a));
                Receive<StreamUpdateMsg>(a => StreamUpdateHandler(a));

                _streamHealthCheckActor.Tell(new StreamConnectedMsg { FixtureId = _resource.Id });

                FixtureState fixtureState = _eventState.GetFixtureState(_resource.Id);

                if (IsSnapshotNeeded(fixtureState))
                {
                    RetrieveAndProcessSnapshot();
                }
                else
                {
                    UnsuspendFixture(fixtureState);
                }

                Stash.UnstashAll();
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Failed moving to Streaming State for resource {_resource} - exception - {ex}");
                Become(Errored);
            }
        }

        //No further messages should be accepted, resource has been disconnected, the actor will be restared
        private void Disconnected()
        {
            State = StreamListenerState.Disconnected;

            //tell Stream Listener Manager Actor that we got disconnected so it can kill and recreate this child actor
            Context.Parent.Tell(new StreamDisconnectedMsg { FixtureId = _fixtureId, Sport = _resource.Sport });
        }

        //Error has occured, resource will try to recover by processing full snapshot
        private void Errored()
        {
            var prevState = State;
            State = StreamListenerState.Errored;

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

                if (prevState == StreamListenerState.Initializing)
                {
                    Become(Stopped);
                }
            }

            Receive<StreamUpdateMsg>(a => RecoverFromErroredState(prevState, out erroredEx));
        }

        //No further messages should be accepted, resource has stopped streaming
        private void Stopped()
        {
            State = StreamListenerState.Stopped;

            //tell Stream Listener Manager Actor that we stopped so it can kill this child actor
            Context.Parent.Tell(new StreamListenerStoppedMsg { FixtureId = _fixtureId });
        }

        #endregion

        #region Message Handlers

        private void StreamUpdateHandler(StreamUpdateMsg msg)
        {
            try
            {
                var deltaMessage = msg.Data.FromJson<StreamMessage>();
                var fixtureDelta = deltaMessage.GetContent<Fixture>();

                _logger.Info($"{fixtureDelta} stream update arrived");

                var fixtureValidationMsg = new FixtureValidationMsg
                {
                    FixtureDetla = fixtureDelta,
                    Sequence = _currentSequence,
                    Epoch = _currentEpoch
                };
                fixtureValidationMsg =
                    _streamHealthCheckActor.Ask<FixtureValidationMsg>(fixtureValidationMsg).Result;

                _currentSequence = fixtureDelta.Sequence;

                if (!fixtureValidationMsg.IsSequenceValid)
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
                _currentEpoch = fixtureDelta.Epoch;

                if (fixtureValidationMsg.IsEpochValid)
                {
                    ProcessSnapshot(fixtureDelta, false, hasEpochChanged);
                    _logger.Info($"Update for {fixtureDelta} processed successfully");
                }
                else
                {
                    ProcessInvalidEpoch(fixtureDelta, hasEpochChanged);
                }
            }
            catch (AggregateException ex)
            {
                int total = ex.InnerExceptions.Count;
                int count = 0;
                foreach (var innerEx in ex.InnerExceptions)
                {
                    _logger.Error($"Error processing update for {_resource} {innerEx} ({++count}/{total})");
                }

                Become(Errored);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing update {_resource} - exception - {ex}");

                Become(Errored);
            }
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            try
            {
                if (ShouldSuspendOnDisconnection())
                {
                    SuspendFixture(SuspensionReason.DISCONNECT_EVENT);
                }

                Become(Disconnected);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing disconnection for {_resource} - exception - {ex}");

                Become(Errored);
            }
        }

        private void ResourceStateUpdateMsgHandler(ResourceStateUpdateMsg msg)
        {
            if (_resource == null || msg.Resource == null || msg.Resource.Id != _resource.Id)
                return;

            try
            {
                _resource.Content.Sequence = msg.Resource.Content.Sequence;
                _resource.Content.MatchStatus = msg.Resource.Content.MatchStatus;

                _logger.Debug(
                    $"Listener state for resource {msg.Resource} has " +
                    $"sequence={msg.Resource.Content.Sequence} " +
                    $"processedSequence={_currentSequence} " +
                    (msg.Resource.Content.Sequence > _currentSequence
                        ? $"missedSequence={msg.Resource.Content.Sequence - _currentSequence} "
                        : "") +
                    $"State={State} " +
                    $"isMatchOver={msg.Resource.IsMatchOver}");

                var streamValidationMsg = new StreamValidationMsg
                {
                    Resource = msg.Resource,
                    State = State,
                    CurrentSequence = _currentSequence
                };
                var streamIsValid = _streamHealthCheckActor.Ask<bool>(streamValidationMsg).Result;
                var isFixtureInSetup = msg.Resource.Content.MatchStatus == (int)MatchStatus.Setup;
                var connectToStreamServer =
                    State != StreamListenerState.Streaming &&
                    (!isFixtureInSetup || _settings.AllowFixtureStreamingInSetupMode);

                if (streamIsValid && connectToStreamServer)
                {
                    ConnectToStreamServer();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing resource state update for {_resource} - exception - {ex}");

                Become(Errored);
            }
        }

        #endregion

        #region Private methods

        private void Initialize()
        {
            try
            {
                State = StreamListenerState.Initializing;

                Receive<StreamConnectedMsg>(a => Become(Streaming));
                Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
                Receive<ResourceStateUpdateMsg>(a => ResourceStateUpdateMsgHandler(a));
                Receive<StreamUpdateMsg>(a => Stash.Stash());

                var fixtureState = _eventState.GetFixtureState(_resource.Id);

                _currentEpoch = fixtureState?.Epoch ?? -1;
                _currentSequence = _resource.Content.Sequence;
                _lastSequenceProcessedInSnapshot = -1;

                if (!string.IsNullOrEmpty(_resource.Content?.StartTime))
                {
                    _fixtureStartTime = DateTime.Parse(_resource.Content.StartTime);
                }

                bool isMatchOver = _resource.MatchStatus == MatchStatus.MatchOver || _resource.IsMatchOver;
                bool processMatchOver =
                    isMatchOver && (fixtureState == null || fixtureState.MatchStatus != MatchStatus.MatchOver);

                if (isMatchOver)
                {
                    _logger.Warn($"Listener will not start for {_resource} as the resource is marked as ended");
                    if (processMatchOver)
                    {
                        ProcessMatchOver();
                    }
                    Become(Stopped);
                }
                else
                {
                    var isFixtureInSetup = _resource.MatchStatus == MatchStatus.Setup;
                    var connectToStreamServer = !isFixtureInSetup || _settings.AllowFixtureStreamingInSetupMode;

                    //either connect to stream server and go to Streaming State, or go to Initialized State
                    if (connectToStreamServer)
                    {
                        ConnectToStreamServer();
                    }
                    else
                    {
                        if (IsSnapshotNeeded(fixtureState))
                        {
                            RetrieveAndProcessSnapshot();
                        }
                        else
                        {
                            UnsuspendFixture(fixtureState);
                        }

                        Become(Initialized);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error on Initialize resource {_resource} - exception - {ex}");

                Become(Errored);
            }
        }

        private void ConnectToStreamServer()
        {
            _logger.Debug($"Starting streaming for {_resource} - resource has sequence={_resource.Content.Sequence}");

            _streamHealthCheckActor.Tell(new ConnectToStreamServerMsg());
            _resourceActor.Tell(new ResourceStartStreamingMsg());

            _logger.Debug($"Started streaming for {_resource} - resource has sequence={_resource.Content.Sequence}");
        }

        private bool IsSnapshotNeeded(FixtureState state)
        {
            _logger.Debug(
                $"{_resource} has stored sequence={state?.Sequence}; resource sequence={_resource.Content.Sequence}");

            return state == null || _resource.Content.Sequence != state.Sequence;
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
                Id = _resource.Id,
                Sequence = -1
            };

            if (state != null)
            {
                fixture.Sequence = state.Sequence;
                fixture.MatchStatus = state.MatchStatus.ToString();
            }

            //unsuspends markets suspended by adapter
            _stateManager.StateProvider.SuspensionManager.Unsuspend(fixture.Id);
            _platformConnector.UnSuspend(fixture);
        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false)
        {
            var snapshot = RetrieveSnapshot();
            ProcessSnapshot(snapshot, true, hasEpochChanged);
        }

        private Fixture RetrieveSnapshot()
        {
            _logger.Debug($"Getting snapshot for {_resource}");

            var snapshotJson = _resource.GetSnapshot();

            if (string.IsNullOrEmpty(snapshotJson))
                throw new Exception($"Received empty snapshot for {_resource}");

            var snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
            if (snapshot == null || snapshot != null && snapshot.Id.IsNullOrWhiteSpace())
                throw new Exception($"Received a snapshot that resulted in an empty snapshot object {_resource}"
                                    + Environment.NewLine +
                                    $"Platform raw data=\"{snapshotJson}\"");

            if (snapshot.Sequence < _currentSequence)
                throw new Exception(
                    $"Received snapshot {snapshot} with sequence lower than currentSequence={_currentSequence}");

            _stats.IncrementValue(AdapterCoreKeys.SNAPSHOT_COUNTER);
            _fixtureStartTime = snapshot.StartTime;

            return snapshot;
        }

        private void ProcessSnapshot(Fixture snapshot, bool isFullSnapshot, bool hasEpochChanged)
        {
            var logString = isFullSnapshot ? "snapshot" : "stream update";

            if (snapshot == null || (snapshot != null && string.IsNullOrWhiteSpace(snapshot.Id)))
                throw new ArgumentException($"Received empty {logString} for {_resource}");

            _logger.Info($"Processing {logString} for {snapshot}");

            Stopwatch timer = new Stopwatch();
            timer.Start();

            try
            {
                if (isFullSnapshot && !VerifySequenceOnSnapshot(snapshot))
                    return;

                _marketsRuleManager.ApplyRules(snapshot);
                snapshot.IsModified = true;

                if (isFullSnapshot)
                    _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);
                else
                    _platformConnector.ProcessStreamUpdate(snapshot, hasEpochChanged);


                UpdateState(snapshot, isFullSnapshot);
            }
            catch (FixtureIgnoredException)
            {
                _logger.Warn($"{_resource} received a FixtureIgnoredException");

                _stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);
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

                _stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);
                throw;
            }
            catch (Exception ex)
            {
                _marketsRuleManager.RollbackChanges();

                _stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);

                _logger.Error($"Error processing {logString} {snapshot}", ex);
                throw;
            }
            finally
            {
                timer.Stop();
                _stats.AddValue(
                    isFullSnapshot ? AdapterCoreKeys.SNAPSHOT_PROCESSING_TIME : AdapterCoreKeys.UPDATE_PROCESSING_TIME,
                    timer.ElapsedMilliseconds.ToString());
            }

            _logger.Info($"Finished processing {logString} for {snapshot}");
        }

        private void ProcessInvalidEpoch(Fixture fixtureDelta, bool hasEpochChanged)
        {
            _fixtureStartTime = fixtureDelta.StartTime ?? _fixtureStartTime;

            if (fixtureDelta.IsDeleted)
            {
                ProcessFixtureDelete(fixtureDelta);
                StopStreaming();
                return;
            }

            if (fixtureDelta.IsMatchStatusChanged)
            {
                if (!string.IsNullOrEmpty(fixtureDelta.MatchStatus))
                {
                    _logger.Debug(
                        $"{_resource} has changed matchStatus={Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus)}");
                    _platformConnector.ProcessMatchStatus(fixtureDelta);
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

            try
            {
                SuspendFixture(SuspensionReason.FIXTURE_DELETED);
                _platformConnector.ProcessFixtureDeletion(fixtureDelta);
            }
            catch (Exception e)
            {
                _logger.Error($"An exception occured while trying to process fixture deletion for {_resource}", e);
            }

            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus);

            //reset event state
            _marketsRuleManager.OnFixtureUnPublished();
            _eventState.UpdateFixtureState(_resource.Sport, fixtureDelta.Id, -1, status, _currentEpoch);
        }

        private void ProcessMatchOver()
        {
            _logger.Info($"{_resource} is Match Over. Suspending all markets and stopping the stream.");

            try
            {
                SuspendAndReprocessSnapshot(true);
                _stateManager.ClearState(_resource.Id);
            }
            catch (Exception ex)
            {
                _logger.Error($"An error occured while trying to process match over resource {_resource.Id} - exception - {ex}");
                throw;
            }
        }

        private void SuspendAndReprocessSnapshot(bool hasEpochChanged = false)
        {
            SuspendFixture(SuspensionReason.SUSPENSION);
            RetrieveAndProcessSnapshot(hasEpochChanged);
        }

        private void SuspendFixture(SuspensionReason reason)
        {
            _logger.Info($"Suspending fixtureId={_resource.Id} due reason={reason}");

            _stateManager.StateProvider.SuspensionManager.Suspend(_resource.Id, reason);
            _platformConnector.Suspend(_resource.Id);
        }

        private void UpdateState(Fixture snapshot, bool isSnapshot = false)
        {
            _marketsRuleManager.CommitChanges();

            var status = (MatchStatus)Enum.Parse(typeof(MatchStatus), snapshot.MatchStatus);

            _eventState.UpdateFixtureState(_resource.Sport, _resource.Id, snapshot.Sequence, status, snapshot.Epoch);

            if (isSnapshot)
            {
                _lastSequenceProcessedInSnapshot = snapshot.Sequence;
            }

            _currentSequence = snapshot.Sequence;
        }

        private void StopStreaming()
        {
            _resourceActor.Tell(new ResourceStopStreamingMsg());

            Become(Stopped);
        }

        private bool ShouldSuspendOnDisconnection()
        {
            var state = _eventState.GetFixtureState(_fixtureId);
            if (state == null || !_fixtureStartTime.HasValue)
                return true;

            var spanBetweenNowAndStartTime = _fixtureStartTime.Value - DateTime.UtcNow;
            var doNotSuspend = _settings.DisablePrematchSuspensionOnDisconnection && spanBetweenNowAndStartTime.TotalMinutes > _settings.PreMatchSuspensionBeforeStartTimeInMins;
            return !doNotSuspend;
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
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Fixture {_resource} failed to recover from Errored State - exception - {ex}");

                erroredEx = ex;
            }
        }

        #endregion
    }
}

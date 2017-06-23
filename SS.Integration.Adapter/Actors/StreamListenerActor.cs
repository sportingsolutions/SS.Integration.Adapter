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

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class is responsible for managing resource and streaming 
    /// </summary>
    public class StreamListenerActor : ReceiveActor
    {
        public enum StreamListenerState
        {
            Initializing,
            Ready,
            Streaming,
            Disconnected,
            Finished
        }

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActor).ToString());
        private readonly ISettings _settings;
        private readonly IResourceFacade _resource;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IEventState _eventState;
        private readonly IStatsHandle _stats;
        private readonly IStateManager _stateManager;
        private readonly IMarketRulesManager _marketsRuleManager;

        private int _currentEpoch;
        private int _currentSequence;
        private int _lastSequenceProcessedInSnapshot;
        private int _startStreamingNotResondingWarnCount;
        private DateTime? _fixtureStartTime;
        private bool _shouldProcessFirstFullSnapshot;

        #endregion

        #region Properties

        internal StreamListenerState State { get; private set; }

        #endregion

        #region Constructors

        public StreamListenerActor(
            IResourceFacade resource,
            IAdapterPlugin platformConnector,
            IEventState eventState,
            IStateManager stateManager,
            ISettings settings)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _platformConnector = platformConnector ?? throw new ArgumentNullException(nameof(platformConnector));
            _eventState = eventState ?? throw new ArgumentNullException(nameof(eventState));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _marketsRuleManager = _stateManager.CreateNewMarketRuleManager(resource.Id);
            _stats = StatsManager.Instance[string.Concat("adapter.core.sport.", resource.Sport)].GetHandle();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Initializing();
        }

        #endregion

        #region Behaviors

        /// <summary>
        /// If fixture has MatchOver State then don't initialize and move to Finished State.
        /// While the first snapshot is being processed it stays in the Initializing state.
        /// After the first snapshot has been processed it can:
        /// - either start streaming and moves to Streaming state
        /// OR
        /// - move to Ready state as not allowed to be streaming yet and waits for a signal to start streaming
        /// </summary>
        private void Initializing()
        {
            State = StreamListenerState.Initializing;
            var fixtureState = _eventState.GetFixtureState(_resource.Id);

            _currentEpoch = fixtureState?.Epoch ?? -1;
            _currentSequence = _resource.Content.Sequence;
            _lastSequenceProcessedInSnapshot = -1;
            if (!string.IsNullOrEmpty(_resource.Content?.StartTime))
            {
                _fixtureStartTime = DateTime.Parse(_resource.Content.StartTime);
            }
            _shouldProcessFirstFullSnapshot = true;

            bool isMatchOver = _resource.MatchStatus == MatchStatus.MatchOver || _resource.IsMatchOver;
            bool processMatchOver =
                isMatchOver && (fixtureState == null || fixtureState.MatchStatus != MatchStatus.MatchOver);

            if (isMatchOver)
            {
                _logger.WarnFormat("Listener will not start for {0} as the resource is marked as ended", _resource);
                if (processMatchOver)
                {
                    ProcessMatchOver();
                }
                Become(Finished);
                return;
            }
            if (fixtureState == null || fixtureState.MatchStatus != _resource.MatchStatus)
            {
                RetrieveAndProcessSnapshot();
            }

            Receive<StreamConnectedMsg>(a => StreamConnectedHandler(a));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedHandler(a));
            Receive<StartStreamingNotRespondingMsg>(a => StartStreamingNotRespondingHandler(a));

            if (!(_resource.MatchStatus == MatchStatus.Ready ||
                _resource.MatchStatus == MatchStatus.Setup && !_settings.AllowFixtureStreamingInSetupMode))
            {
                ConnectToStreamServer();
            }
            else
            {
                Become(Ready);
            }
        }

        //Initialised but not streaming yet - this can happen when you start fixture in Setup
        private void Ready()
        {
            State = StreamListenerState.Ready;
            Receive<TakeSnapshotMsg>(o =>
            {
                if (_shouldProcessFirstFullSnapshot)
                {
                    RetrieveAndProcessSnapshot();
                }
            });
            Receive<StreamUpdateMsg>(a => StreamUpdateHandler(a));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedHandler(a));

            Self.Tell(new TakeSnapshotMsg());
        }

        //Connected and streaming state - all messages should be processed
        private void Streaming()
        {
            State = StreamListenerState.Streaming;
            Receive<TakeSnapshotMsg>(o =>
            {
                if (_shouldProcessFirstFullSnapshot)
                {
                    RetrieveAndProcessSnapshot();
                }
            });
            Receive<StreamUpdateMsg>(a => StreamUpdateHandler(a));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedHandler(a));

            Self.Tell(new TakeSnapshotMsg());
            // Sends feed messages to plugin for processing 
            // Sends messages to healthcheck Actor to validate time and sequences
        }

        //Suspends the fixture and sends message to Stream Listener Manager
        private void Disconnected()
        {
            //All futher messages are discarded
            //StreamDisconnectedMessage
            State = StreamListenerState.Disconnected;

            _resource.StopStreaming();
        }

        //Match over has been processed no further messages should be accepted 
        private void Finished()
        {
            State = StreamListenerState.Finished;
            //Match over arrived it should disconnect and let StreamListenerManager now it's completed
        }

        #endregion

        #region Private methods

        private void ConnectToStreamServer()
        {
            _logger.DebugFormat("Starting streaming for {0} - resource has sequence={1}", _resource, _resource.Content.Sequence);
            StartStreamingWithChecking(() => _resource.StartStreaming(), _resource);
            _logger.DebugFormat("Started streaming for {0} - resource has sequence={1}", _resource, _resource.Content.Sequence);
        }

        private void StreamConnectedHandler(StreamConnectedMsg msg)
        {
            //Check for the correct fixture, but this should never happen as the Resource Actor should be a child of the Stream Listener Actor
            if (msg.FixtureId != _resource.Id)
            {
                return;
            }

            Become(Streaming);
        }

        private void StreamDisconnectedHandler(StreamDisconnectedMsg msg)
        {
            //Check for the correct fixture, but this should never happen as the Resource Actor should be a child of the Stream Listener Actor
            if (msg.FixtureId != _resource.Id)
            {
                return;
            }

            Become(Disconnected);
        }

        private void StreamUpdateHandler(StreamUpdateMsg msg)
        {
            var deltaMessage = msg.Data.FromJson<StreamMessage>();
            var fixtureDelta = deltaMessage.GetContent<Fixture>();

            _logger.InfoFormat($"{fixtureDelta} stream update arrived");

            //Check for the correct fixture, but this should never happen as the Resource Actor should be a child of the Stream Listener Actor
            if (fixtureDelta.Id != _resource.Id)
            {
                _logger.WarnFormat($"{fixtureDelta} stream update rejected as it links to a different resource!");
                return;
            }

            if (!IsSequenceValid(fixtureDelta))
            {
                _logger.WarnFormat("Update for {0} will not be processed because sequence is not valid", fixtureDelta);

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
                _fixtureStartTime = fixtureDelta.StartTime ?? _fixtureStartTime;

                if (fixtureDelta.IsMatchStatusChanged && !string.IsNullOrEmpty(fixtureDelta.MatchStatus))
                {
                    _logger.DebugFormat("{0} has changed matchStatus={1}", _resource,
                        Enum.Parse(typeof(MatchStatus), fixtureDelta.MatchStatus));
                    _platformConnector.ProcessMatchStatus(fixtureDelta);
                }

                if ((fixtureDelta.IsMatchStatusChanged && fixtureDelta.IsMatchOver) || fixtureDelta.IsDeleted)
                {

                    if (fixtureDelta.IsDeleted)
                    {
                        ProcessFixtureDelete(fixtureDelta);
                    }
                    else // Match Over
                    {
                        ProcessMatchOver();
                    }

                    return;
                }


                _logger.InfoFormat("Stream update {0} has epoch change with reason {1}, the snapshot will be processed instead.",
                    fixtureDelta,
                    //aggregates LastEpochChange reasons into string like "BaseVariables,Starttime"
                    fixtureDelta.LastEpochChangeReason.Select(x => ((EpochChangeReason)x).ToString()).Aggregate((first, second) => $"{first}, {second}"));

                SuspendAndReprocessSnapshot(hasEpochChanged);
                return;
            }

            _logger.InfoFormat("Update fo {0} processed successfully", fixtureDelta);
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

            //the epoch change reason can contain multiple reasons
            if (fixtureDelta.IsStartTimeChanged && fixtureDelta.LastEpochChangeReason.Length == 1)
            {
                _logger.InfoFormat("{0} has had its start time changed", fixtureDelta);
                return true;
            }

            return false;
        }

        private bool VerifySequenceOnSnapshot(Fixture snapshot)
        {
            if (snapshot.Sequence < _lastSequenceProcessedInSnapshot)
            {
                _logger.WarnFormat("Newer snapshot {0} was already processed on another thread, current sequence={1}", snapshot,
                    _currentSequence);
                return false;
            }

            return true;
        }

        private void StartStreamingWithChecking(Action action, object obj)
        {
            var interval = _settings.StartStreamingTimeoutInSeconds;
            if (interval <= 0)
                interval = 1;

            _startStreamingNotResondingWarnCount = 0;
            ICancelable startStreamingNotResponding =
                Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    interval * 1000,
                    interval * 1000,
                    Self,
                    new StartStreamingNotRespondingMsg(),
                    Self);

            action();

            startStreamingNotResponding.Cancel();
            Self.Tell(new StreamConnectedMsg { FixtureId = _resource.Id });
        }

        private void StartStreamingNotRespondingHandler(StartStreamingNotRespondingMsg msg)
        {
            var unresponsiveTime = _startStreamingNotResondingWarnCount * _settings.StartStreamingTimeoutInSeconds;
            _logger.Warn(
                $"StartStreaming for {_resource} did't respond for {unresponsiveTime} seconds. " +
                "Possible network problem or port 5672 is locked");
        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false)
        {
            FixtureState state = _eventState.GetFixtureState(_resource.Id);
            int savedResourceSequence = -1;
            if (state != null)
            {
                savedResourceSequence = state.Sequence;
            }

            _logger.DebugFormat("{0} has stored sequence={1} and resource current sequence={2}", _resource, savedResourceSequence, _resource.Content.Sequence);

            if (savedResourceSequence == -1 || _resource.Content.Sequence != savedResourceSequence)
            {
                var snapshot = RetrieveSnapshot();
                if (snapshot != null)
                {
                    ProcessSnapshot(snapshot, true, hasEpochChanged);
                }
            }
            else
            {
                Fixture fixture = new Fixture
                {
                    Id = _resource.Id,
                    Sequence = savedResourceSequence
                };

                if (state != null)
                    fixture.MatchStatus = state.MatchStatus.ToString();

                //unsuspends markets suspended by adapter
                _stateManager.StateProvider.SuspensionManager.Unsuspend(fixture.Id);
                _platformConnector.UnSuspend(fixture);
            }

            _shouldProcessFirstFullSnapshot = false;
        }

        private Fixture RetrieveSnapshot()
        {
            _logger.DebugFormat("Getting snapshot for {0}", _resource);

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

            _logger.InfoFormat("Processing {0} for {1}", logString, snapshot);

            Stopwatch timer = new Stopwatch();
            timer.Start();

            try
            {
                if (isFullSnapshot && !VerifySequenceOnSnapshot(snapshot)) return;

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
                _logger.WarnFormat("{0} received a FixtureIgnoredException", _resource);

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
            }
            catch (Exception ex)
            {
                _marketsRuleManager.RollbackChanges();

                _stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);

                _logger.Error($"Error processing {logString} {snapshot}", ex);
            }
            finally
            {
                timer.Stop();
                _stats.AddValue(
                    isFullSnapshot ? AdapterCoreKeys.SNAPSHOT_PROCESSING_TIME : AdapterCoreKeys.UPDATE_PROCESSING_TIME,
                    timer.ElapsedMilliseconds.ToString());
            }

            _logger.InfoFormat("Finished processing {0} for {1}", logString, snapshot);
        }

        private void ProcessFixtureDelete(Fixture fixtureDelta)
        {
            _logger.InfoFormat("{0} has been deleted from the GTP Fixture Factory. Suspending all markets and stopping the stream.", _resource);

            try
            {
                SuspendFixture(SuspensionReason.FIXTURE_DELETED);
                _platformConnector.ProcessFixtureDeletion(fixtureDelta);
                Self.Tell(new StreamDisconnectedMsg { FixtureId = _resource.Id });
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
            _logger.InfoFormat("{0} is Match Over. Suspending all markets and stopping the stream.", _resource);

            try
            {
                SuspendAndReprocessSnapshot(true);
                _stateManager.ClearState(_resource.Id);
                Self.Tell(new StreamDisconnectedMsg { FixtureId = _resource.Id });
            }
            catch (Exception e)
            {
                _logger.Error($"An error occured while trying to process match over resource {_resource.Id}", e);
            }
        }

        private void SuspendAndReprocessSnapshot(bool hasEpochChanged = false)
        {
            SuspendFixture(SuspensionReason.SUSPENSION);
            RetrieveAndProcessSnapshot(hasEpochChanged);
        }

        private void SuspendFixture(SuspensionReason reason)
        {
            _logger.InfoFormat("Suspending fixtureId={0} due reason={1}", _resource.Id, reason);

            _stateManager.StateProvider.SuspensionManager.Suspend(_resource.Id, reason);
        }

        private bool ShouldSuspendOnDisconnection()
        {
            var state = _eventState.GetFixtureState(_resource.Id);
            if (state == null || !_fixtureStartTime.HasValue)
                return true;

            var spanBetweenNowAndStartTime = _fixtureStartTime.Value - DateTime.UtcNow;
            var doNotSuspend = _settings.DisablePrematchSuspensionOnDisconnection && spanBetweenNowAndStartTime.TotalMinutes > _settings.PreMatchSuspensionBeforeStartTimeInMins;
            return !doNotSuspend;
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

        #endregion

        #region Private messages

        private class TakeSnapshotMsg
        {
        }

        private class StartStreamingNotRespondingMsg
        {
        }

        #endregion
    }

    #region Internal messages

    internal class StreamConnectedMsg
    {
        public string FixtureId { get; set; }
    }

    internal class StreamDisconnectedMsg
    {
        public string FixtureId { get; set; }
    }

    internal class StreamUpdateMsg
    {
        public string Data { get; set; }
    }

    internal class StreamHealthCheckMsg
    {
        public string FixtureId { get; set; }

        public int Sequence { get; set; }

        public DateTime Received { get; set; }
    }

    #endregion
}

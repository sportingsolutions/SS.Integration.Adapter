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

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class is responsible for managing resource and streaming 
    /// </summary>
    public class StreamListenerActor : ReceiveActor
    {
        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActor).ToString());
        private readonly IResourceFacade _resource;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IEventState _eventState;
        private readonly IStatsHandle _stats;
        private readonly IMarketRulesManager _marketsRuleManager;

        private int _currentSequence;
        private int _lastSequenceProcessedInSnapshot;

        #endregion

        #region Constructors

        public StreamListenerActor(
            IResourceFacade resource,
            IAdapterPlugin platformConnector,
            IEventState eventState,
            IStateManager stateManager)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _platformConnector = platformConnector ?? throw new ArgumentNullException(nameof(platformConnector));
            _eventState = eventState ?? throw new ArgumentNullException(nameof(eventState));
            _marketsRuleManager = stateManager?.CreateNewMarketRuleManager(resource.Id) ?? throw new ArgumentNullException(nameof(stateManager));
            _stats = StatsManager.Instance[string.Concat("adapter.core.sport.", resource.Sport)].GetHandle();

            Initializing();
        }

        #endregion

        #region Behaviors

        /// <summary>
        /// While the first snapshot is being processed it stays in the Initializing state.
        /// After the first snapshot has been processed it can:
        /// - either start streaming and moves to Streaming state
        /// OR
        /// - move to Ready state as not allowed to be streaming yet and waits for a signal to start streaming
        /// </summary>
        private void Initializing()
        {
            Receive<TakeSnapshotMsg>(o => RetrieveAndProcessSnapshot());
            Self.Tell(new TakeSnapshotMsg());
        }

        //Initialised but not streaming yet - this can happen when you start fixture in Setup
        private void Ready()
        {
            
        }

        //Connected and streaming state - all messages should be processed
        private void Streaming()
        {
            // Sends feed messages to plugin for processing 
            // Sends messages to healthcheck Actor to validate time and sequences
        }

        //Suspends the fixture and sends message to Stream Listener Manager
        private void Disconnected()
        {
            //All futher messages are discarded
            //StreamDisconnectedMessage

        }

        //Match over has been processed no further messages should be accepted 
        private void Finished()
        {
            //Match over arrived it should disconnect and let StreamListenerManager now it's completed
        }

        #endregion

        #region Private methods

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

        private void StartStreaming()
        {

        }

        private void StopStreaming()
        {

        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false)
        {
            var snapshot = RetrieveSnapshot();
            if (snapshot != null)
            {
                ProcessSnapshot(snapshot, true, hasEpochChanged);
            }
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

        #endregion
    }

    #region Internal messages

    internal class StartStreamingMsg
    {
        public string FixtureId { get; set; }
    }

    internal class StreamDisconnectedMsg
    {
        public string FixtureId { get; set; }
    }

    internal class StreamHealthCheckMsg
    {
        public string FixtureId { get; set; }

        public int Sequence { get; set; }

        public DateTime Received { get; set; }
    }

    #endregion
}

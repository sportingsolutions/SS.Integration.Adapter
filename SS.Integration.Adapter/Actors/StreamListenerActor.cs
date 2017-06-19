using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Exceptions;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Stats.Keys;

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
        private readonly IMarketRulesManager _marketsRuleManager;

        #endregion

        #region Constructors

        public StreamListenerActor(
            IResourceFacade resource, 
            IAdapterPlugin platformConnector, 
            IEventState eventState, 
            IStateManager stateManager, 
            ISettings settings)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (platformConnector == null)
            {
                throw new ArgumentNullException(nameof(platformConnector));
            }
            if (eventState == null)
            {
                throw new ArgumentNullException(nameof(eventState));
            }
            if (stateManager == null)
            {
                throw new ArgumentNullException(nameof(stateManager));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _resource = resource;
            _platformConnector = platformConnector;
            _marketsRuleManager = stateManager.CreateNewMarketRuleManager(resource.Id);

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
            //All messages are stashed until it changes state
            //Once completed it sends message
            //StreamListenerCreationCompletedMessage() -> StreamListenerBuilderActor

            //Create HealthCheckActor()

            Receive<TakeSnapshotMsg>(o => ProcessSnapshot());

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

        private void ProcessSnapshot(Fixture snapshot, bool isFullSnapshot, bool hasEpochChanged, bool setErrorState = true, bool skipMarketRules = false)
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

                if (!skipMarketRules)
                {
                    _marketsRuleManager.ApplyRules(snapshot);

                    snapshot.IsModified = true;
                }
                else
                {
                    _marketsRuleManager.ApplyRules(snapshot, isRemovalDisabled: true);
                }

                if (isFullSnapshot)
                    _platformConnector.ProcessSnapshot(snapshot, hasEpochChanged);
                else
                    _platformConnector.ProcessStreamUpdate(snapshot, hasEpochChanged);


                UpdateState(snapshot, isFullSnapshot);
            }
            catch (FixtureIgnoredException ex)
            {
                _logger.WarnFormat("{0} received a FixtureIgnoredException", _resource);
                IsIgnored = true;

                _Stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);
                RaiseEvent(OnError, ex);
            }
            catch (AggregateException ex)
            {
                _marketsRuleManager.RollbackChanges();

                int total = ex.InnerExceptions.Count;
                int count = 0;
                foreach (var e in ex.InnerExceptions)
                {
                    _logger.Error(string.Format("Error processing {0} for {1} ({2}/{3})", logString, snapshot, ++count, total), e);
                }

                _Stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);
                RaiseEvent(OnError, ex);

                if (setErrorState)
                    SetErrorState();
                else
                    throw;
            }
            catch (Exception ex)
            {
                _marketsRuleManager.RollbackChanges();

                _Stats.IncrementValue(AdapterCoreKeys.ERROR_COUNTER);

                _logger.Error(string.Format("Error processing {0} {1}", logString, snapshot), ex);

                RaiseEvent(OnError, ex);

                if (setErrorState)
                    SetErrorState();
                else
                    throw;
            }
            finally
            {
                _isProcessiongAtPluginSide = false;
                timer.Stop();
                if (isFullSnapshot)
                    _Stats.AddValue(AdapterCoreKeys.SNAPSHOT_PROCESSING_TIME, timer.ElapsedMilliseconds.ToString());
                else
                    _Stats.AddValue(AdapterCoreKeys.UPDATE_PROCESSING_TIME, timer.ElapsedMilliseconds.ToString());
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
                _currentEpoch = snapshot.Epoch;
            }

            _currentSequence = snapshot.Sequence;
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

        private void StartStreaming()
        {

        }

        private void StopStreaming()
        {

        }

        private void RetrieveAndProcessSnapshot(bool hasEpochChanged = false, bool skipMarketRules = false)
        {
            var snapshot = RetrieveSnapshot();
            if (snapshot != null)
            {
                var shouldSkipProcessingMarketRules = skipMarketRules || (_settings.SkipRulesOnError && IsErrored);
                ProcessSnapshot(snapshot, true, hasEpochChanged, !IsErrored, shouldSkipProcessingMarketRules);
            }
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

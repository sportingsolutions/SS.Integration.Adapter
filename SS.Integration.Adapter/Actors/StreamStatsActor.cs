using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Exceptions;

namespace SS.Integration.Adapter.Actors
{
    // This actor main responsibility is logging how long did it take to process snapshots / stream updates with how many markets 
    // any errors should be also counted and logged by fixture
    public class StreamStatsActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(StreamStatsActor);
        public const string ApiExceptionType = nameof(ApiException);
        public const string PluginExceptionType = nameof(PluginException);
        public const string GenericExceptionType = "GenericException";

        #endregion

        #region Attributes

        private ILog _logger = LogManager.GetLogger(typeof(StreamStatsActor));
        private UpdateStatsStartMsg _startMessage;
        private static int _snapshotsCount;
        private static int _streamUpdatesCount;
        private static Dictionary<string, int> _errorsCount;
        private static DateTime _lastDisconnectedDate;
        private static int _disconnectionsCount;
        private static DateTime _adapterStartDate = DateTime.UtcNow;

        #endregion

        #region Constructors

        public StreamStatsActor()
        {
            _errorsCount = new Dictionary<string, int>
            {
                {ApiExceptionType, 0},
                {PluginExceptionType, 0},
                {GenericExceptionType, 0}
            };

            Receive<UpdateStatsStartMsg>(a => UpdateStatsStartMsgHandler(a));
            Receive<UpdateStatsErrorMsg>(a => UpdateStatsErrorMsgHandler(a));
            Receive<UpdateStatsFinishMsg>(a => UpdateStatsFinishMsgHandler(a));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
        }

        #endregion

        #region Message Handlers

        private void UpdateStatsStartMsgHandler(UpdateStatsStartMsg msg)
        {
            _startMessage = msg;
        }

        private void UpdateStatsErrorMsgHandler(UpdateStatsErrorMsg msg)
        {
            var errType = msg.Error.GetType().Name;
            if (_errorsCount.ContainsKey(errType))
            {
                _errorsCount[errType]++;
            }
            else
            {
                _errorsCount[GenericExceptionType]++;
            }

            _logger.Error(
                $"Error occured at {msg.ErrorOccuredAt} for resource {_startMessage.Fixture.Id} sequence {_startMessage.Sequence} - {msg.Error}");

            var minutes = (int)Math.Ceiling((DateTime.UtcNow - _adapterStartDate).TotalMinutes);

            _logger.Warn($"Number of API errors: {_errorsCount[ApiExceptionType]}");
            _logger.Warn($"Number of Plugin errors: {_errorsCount[PluginExceptionType]}");
            _logger.Warn($"Number of Generic errors: {_errorsCount[GenericExceptionType]}");
            _logger.Warn($"Number of API errors per minute: {_errorsCount[ApiExceptionType] / minutes}");
            _logger.Warn($"Number of Plugin errors per minute: {_errorsCount[PluginExceptionType] / minutes}");
        }

        private void UpdateStatsFinishMsgHandler(UpdateStatsFinishMsg msg)
        {
            if (_startMessage.IsSnapshot)
            {
                _snapshotsCount++;
            }
            else
            {
                _streamUpdatesCount++;
            }

            var timeTaken = msg.CompletedAt - _startMessage.UpdateReceivedAt;

            var updateOrSnapshot = _startMessage.IsSnapshot ? "Snapshot" : "Update";

            var minutes = (int)Math.Ceiling((DateTime.UtcNow - _adapterStartDate).TotalMinutes);

            _logger.Info($"{updateOrSnapshot} for {_startMessage.Fixture}, took processingTime={timeTaken.TotalSeconds} seconds at sequence={_startMessage.Sequence}");
            _logger.Info($"Snapshots processed: {_snapshotsCount}");
            _logger.Info($"Stream updates processed: {_streamUpdatesCount}");
            _logger.Info($"Snapshots per minute: {_snapshotsCount / minutes}");
            _logger.Info($"Stream updates per minute: {_streamUpdatesCount / minutes}");
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            _disconnectionsCount++;
            _lastDisconnectedDate = DateTime.UtcNow;

            var days = (DateTime.UtcNow - _adapterStartDate).TotalDays;
            var weeks = days > 7 ? (int)(days / 7) : 1;

            _logger.Info($"Stream got disconnected at {_lastDisconnectedDate}");
            _logger.Info($"Detected {_disconnectionsCount / weeks} Stream disconnections / week");
        }

        #endregion
    }
}

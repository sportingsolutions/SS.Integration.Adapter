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

using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Event;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Exceptions;

namespace SS.Integration.Adapter.Actors
{
    // This actor main responsibility is logging how long did it take to process snapshots / stream updates with how many markets 
    // any errors should be also counted and logged by fixture
    public class StreamStatsActor : ReceiveActor, ILogReceive
    {
        #region Constants

        public const string ActorName = nameof(StreamStatsActor);
        public const string ApiExceptionType = nameof(ApiException);
        public const string PluginExceptionType = nameof(PluginException);
        public const string GenericExceptionType = "GenericException";

        #endregion

        #region Properties

        internal int SnapshotsCount => _snapshotsCount;
        internal int StreamUpdatesCount => _streamUpdatesCount;
        internal Dictionary<string, int> ErrorsCount => _errorsCount;
        internal int DisconnectionsCount => _disconnectionsCount;
        internal DateTime LastDisconnectedDate => _lastDisconnectedDate;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamStatsActor));
        private readonly Stack<UpdateStatsStartMsg> _startMessagesStack;
        private int _snapshotsCount;
        private int _streamUpdatesCount;
        private readonly Dictionary<string, int> _errorsCount;
        private DateTime _lastDisconnectedDate;
        private int _disconnectionsCount;
        private static readonly DateTime AdapterStartDate = DateTime.UtcNow;

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public StreamStatsActor()
        {
            _errorsCount = new Dictionary<string, int>
            {
                {ApiExceptionType, 0},
                {PluginExceptionType, 0},
                {GenericExceptionType, 0}
            };
            _startMessagesStack = new Stack<UpdateStatsStartMsg>();

            //the order of the Receive messages is very important
            //if there is a hierarcy of classes, then message classes need to be registered in top-down order
            //e.g. if ChildClass inhertis from BaseClass, then register first ChildClass and after that BaseClass
            Receive<UpdatePluginStatsStartMsg>(a => UpdateStatsStartMsgHandler(a));
            Receive<UpdatePluginStatsFinishMsg>(a => UpdatePluginStatsFinishMsgHandler(a));
            Receive<UpdateStatsStartMsg>(a => UpdateStatsStartMsgHandler(a));
            Receive<UpdateStatsFinishMsg>(a => UpdateStatsFinishMsgHandler(a));
            Receive<UpdateStatsErrorMsg>(a => UpdateStatsErrorMsgHandler(a));
            Receive<StreamDisconnectedMsg>(a => StreamDisconnectedMsgHandler(a));
        }

        #endregion

        #region Message Handlers

        private void UpdateStatsStartMsgHandler(UpdateStatsStartMsg msg)
        {
            _startMessagesStack.Push(msg);
        }

        private void UpdateStatsErrorMsgHandler(UpdateStatsErrorMsg msg)
        {
            var startMessage = GetStartMessageObject<UpdateStatsStartMsg>();
            _startMessagesStack.Clear();

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
                $"Error occured at {msg.ErrorOccuredAt} for {startMessage.Fixture} sequence {startMessage.Sequence} - {msg.Error}");

            var minutes = (int)Math.Ceiling((DateTime.UtcNow - AdapterStartDate).TotalMinutes);
            if (minutes == 0)
                return;
            
            _logger.Warn($"Number of API_Errors={_errorsCount[ApiExceptionType]}");
            _logger.Warn($"Number of Plugin_Errors={_errorsCount[PluginExceptionType]}");
            _logger.Warn($"Number of Generic_Errors={_errorsCount[GenericExceptionType]}");

            var _errorsCount_ApiExceptionType =
                _errorsCount[ApiExceptionType] == 0 ? 0 : _errorsCount[ApiExceptionType] / minutes;
            var _errorsCount_PluginExceptionType =
                _errorsCount[PluginExceptionType] == 0 ? 0 : _errorsCount[PluginExceptionType] / minutes;

            _logger.Warn($"Number of API_Errors_PerMinute={_errorsCount_ApiExceptionType}");
            _logger.Warn($"Number of Plugin_Errors_PerMinute={_errorsCount_PluginExceptionType}");
        }

        private void UpdatePluginStatsFinishMsgHandler(UpdatePluginStatsFinishMsg msg)
        {
            var startMessage = GetStartMessageObject<UpdatePluginStatsStartMsg>();
            if (startMessage != null)
            {
                var timeTaken = msg.CompletedAt - startMessage.UpdateReceivedAt;
                _logger.Info($"Plugin {startMessage.PluginMethod} for {startMessage.Fixture}, took processingTime={timeTaken.TotalSeconds} seconds at sequence={startMessage.Sequence}");
            }
        }

        private void UpdateStatsFinishMsgHandler(UpdateStatsFinishMsg msg)
        {
            var startMessage = GetStartMessageObject<UpdateStatsStartMsg>();

            if (startMessage.IsSnapshot)
            {
                _snapshotsCount++;
            }
            else
            {
                _streamUpdatesCount++;
            }

            var timeTaken = msg.CompletedAt - startMessage.UpdateReceivedAt;

            var updateOrSnapshot = startMessage.IsSnapshot ? "Snapshot" : "Update";

            var minutes = (int)Math.Ceiling((DateTime.UtcNow - AdapterStartDate).TotalMinutes);

            if (minutes == 0)
                return;

            var _snapshots_perminute = _snapshotsCount == 0 ? 0 : _snapshotsCount / minutes;
            var _streamupdates_perminute = _streamUpdatesCount == 0 ? 0 : _streamUpdatesCount / minutes;

            _logger.Info($"{updateOrSnapshot} for {startMessage.Fixture}, took processingTime={timeTaken.TotalSeconds} seconds at sequence={startMessage.Sequence}");
            _logger.Info($"{startMessage.Fixture} -> Snapshots_Processed={_snapshotsCount}");
            _logger.Info($"{startMessage.Fixture} -> StreamUpdates_Processed={_streamUpdatesCount}");
            _logger.Info($"{startMessage.Fixture} -> Snapshots_PerMinute={_snapshots_perminute}");
            _logger.Info($"{startMessage.Fixture} -> StreamUpdates_PerMinute={_streamupdates_perminute}");
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            _disconnectionsCount++;
            _lastDisconnectedDate = DateTime.UtcNow;

            var days = (DateTime.UtcNow - AdapterStartDate).TotalDays;
            var weeks = days > 7 ? (int)(days / 7) : 1;

            _logger.Info($"Stream got disconnected at {_lastDisconnectedDate}");
            _logger.Info($"Detected {_disconnectionsCount / weeks} Stream disconnections / week");
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

        private T GetStartMessageObject<T>() where T : UpdateStatsStartMsg
        {
            var startMessage = _startMessagesStack.Pop();
            var startMessageCast = startMessage as T;
            if (startMessageCast == null)
            {
                _logger.Warn($"startMessage wrong type detected - actual type is {startMessage.GetType().FullName}");
            }

            return startMessageCast;
        }

        #endregion
    }
}

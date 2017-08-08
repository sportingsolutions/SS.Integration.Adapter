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
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responsability to check for stream connection state 
    /// by repeated self scheduled message at predetermined interval
    /// - if the streaming server is not responding when trying to connect 
    ///   then appropriate message is logged and stream listener actor is stopped after multiple retries
    /// - if the stream listener got disconnected this class can re-establish the connection 
    ///   when it gets the health check message from the stream listener manager
    /// </summary>
    public class StreamHealthCheckActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(StreamHealthCheckActor);

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActor).ToString());
        private readonly IResourceFacade _resource;
        private readonly ISettings _settings;
        private readonly IStreamHealthCheckValidation _streamHealthCheckValidation;
        private ICancelable _startStreamingNotResponding;
        private int _startStreamingNotRespondingWarnCount;

        #endregion

        #region Constructors

        public StreamHealthCheckActor(
            IResourceFacade resource,
            ISettings settings,
            IStreamHealthCheckValidation streamHealthCheckValidation)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _streamHealthCheckValidation = streamHealthCheckValidation ?? throw new ArgumentNullException(nameof(streamHealthCheckValidation));

            Receive<ConnectToStreamServerMsg>(a => ConnectToStreamServerMsgHandler(a));
            Receive<StreamConnectedMsg>(a => StreamConnectedMsgHandler(a));
            Receive<StartStreamingNotRespondingMsg>(a => StartStreamingNotRespondingMsgHandler(a));
            Receive<StreamHealthCheckMsg>(a => StreamHealthCheckMsgHandler(a));
        }

        #endregion

        #region Message Handlers

        private void StreamHealthCheckMsgHandler(StreamHealthCheckMsg msg)
        {
            if (_resource == null || msg.Resource == null || msg.Resource.Id != _resource.Id)
                return;

            try
            {
                _resource.Content.Sequence = msg.Resource.Content.Sequence;
                _resource.Content.MatchStatus = msg.Resource.Content.MatchStatus;

                _logger.Debug(
                    $"Listener state for {msg.Resource} has " +
                    $"sequence={msg.Resource.Content.Sequence} " +
                    $"processedSequence={msg.CurrentSequence} " +
                    (msg.Resource.Content.Sequence > msg.CurrentSequence
                        ? $"missedSequence={msg.Resource.Content.Sequence - msg.CurrentSequence} "
                        : "") +
                    $"State={msg.StreamingState} " +
                    $"isMatchOver={msg.Resource.IsMatchOver}");

                var streamIsValid =
                    _streamHealthCheckValidation.ValidateStream(msg.Resource, msg.StreamingState, msg.CurrentSequence);

                if (!streamIsValid)
                {
                    _logger.Warn($"Detected invalid stream for {msg.Resource}");
                }

                var connectToStreamServer =
                    _streamHealthCheckValidation.CanConnectToStreamServer(msg.Resource, msg.StreamingState);

                if (streamIsValid && connectToStreamServer)
                {
                    Context.Parent.Tell(new ConnectToStreamServerMsg());
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured on Stream Health Check for {_resource} - exception - {ex}");
                throw;
            }
        }

        private void ConnectToStreamServerMsgHandler(ConnectToStreamServerMsg msg)
        {
            var interval = _settings.StartStreamingTimeoutInSeconds;
            if (interval <= 0)
                interval = 1;

            _startStreamingNotRespondingWarnCount = 0;
            _startStreamingNotResponding =
                Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    interval * 1000,
                    interval * 1000,
                    Self,
                    new StartStreamingNotRespondingMsg { FixtureId = _resource.Id },
                    Self);
        }

        private void StreamConnectedMsgHandler(StreamConnectedMsg msg)
        {
            _startStreamingNotResponding?.Cancel();
            _startStreamingNotResponding = null;
        }

        private void StartStreamingNotRespondingMsgHandler(StartStreamingNotRespondingMsg msg)
        {
            _startStreamingNotRespondingWarnCount += 1;
            var unresponsiveTime = _startStreamingNotRespondingWarnCount * _settings.StartStreamingTimeoutInSeconds;
            _logger.Warn(
                $"StartStreaming for {_resource} did't respond for {unresponsiveTime} seconds. " +
                "Possible network problem or port 5672 is locked");

            if (_startStreamingNotRespondingWarnCount > _settings.StartStreamingAttempts)
            {
                _startStreamingNotResponding.Cancel();
                var streamListenerManagerActor = Context.System.ActorSelection(StreamListenerManagerActor.Path);
                streamListenerManagerActor.Tell(msg);
            }
        }

        #endregion
    }
}

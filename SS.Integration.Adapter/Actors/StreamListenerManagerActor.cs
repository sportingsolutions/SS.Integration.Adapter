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
using System.Linq;
using Akka.Actor;
using log4net;
using SportingSolutions.Udapi.Sdk;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Exceptions;
using SportingSolutions.Udapi.Sdk.Model.Message;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;
using SdkErrorMessage = SportingSolutions.Udapi.Sdk.Events.SdkErrorMessage;

namespace SS.Integration.Adapter.Actors
{
    
    
    
    //This actor manages all StreamListeners 
    public class StreamListenerManagerActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(StreamListenerManagerActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerManagerActor).ToString());
        private readonly ISettings _settings;
        private readonly IActorRef _streamListenerBuilderActorRef;
        private bool _shouldSendProcessSportsMessage;
        private readonly Dictionary<string, Dictionary<string, StreamListenerState>> _streamListeners;
        private readonly ICancelable _logPublishedFixturesCountsMsgSchedule;

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        /// <param name="suspensionManager"></param>
        /// <param name="streamHealthCheckValidation"></param>
        /// <param name="fixtureValidation"></param>
        public StreamListenerManagerActor(
            ISettings settings,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            ISuspensionManager suspensionManager,
            IStreamHealthCheckValidation streamHealthCheckValidation,
            IFixtureValidation fixtureValidation)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (adapterPlugin == null)
                throw new ArgumentNullException(nameof(adapterPlugin));
            if (stateManager == null)
                throw new ArgumentNullException(nameof(stateManager));
            if (suspensionManager == null)
                throw new ArgumentNullException(nameof(suspensionManager));

            _shouldSendProcessSportsMessage = true;

            _streamListenerBuilderActorRef =
                Context.ActorOf(Props.Create(() =>
                        new StreamListenerBuilderActor(
                            settings,
                            Context,
                            adapterPlugin,
                            stateManager,
                            suspensionManager,
                            streamHealthCheckValidation,
                            fixtureValidation)),
                    StreamListenerBuilderActor.ActorName);

            _streamListeners = new Dictionary<string, Dictionary<string, StreamListenerState>>();

            _logPublishedFixturesCountsMsgSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                Self,
                new LogPublishedFixturesCountsMsg(),
                Self);

            Receive<ProcessResourceMsg>(o => ProcessResourceMsgHandler(o));
            Receive<StreamConnectedMsg>(o => StreamConnectedMsgHandler(o));
            Receive<StreamDisconnectedMsg>(o => StreamDisconnectedMsgHandler(o));
            Receive<StreamListenerStoppedMsg>(o => StreamListenerStoppedMsgHandler(o));
            Receive<StartStreamingNotRespondingMsg>(o => StopStreamListenerChildActor(o.FixtureId));
            Receive<StreamListenerInitializedMsg>(o => StreamListenerInitializedMsgHandler(o));
            Receive<StreamListenerCreationFailedMsg>(o => StreamListenerCreationFailedMsgHandler(o));
            Receive<StreamListenerCreationCancelledMsg>(o => StreamListenerCreationCancelledMsgHandler(o));
            Receive<Terminated>(o => TerminatedHandler(o));
            Receive<ResetSendProcessSportsMsg>(o => ResetSendProcessSportsMsgHandler(o));
            Receive<RetrieveAndProcessSnapshotMsg>(o => RetrieveAndProcessSnapshotMsgHandler(o));
            Receive<RestartStreamListenerMsg>(o => RestartStreamListenerMsgHandler(o));
            Receive<ClearFixtureStateMsg>(o => ClearFixtureStateMsgHandler(o));
            Receive<NewStreamListenerActorMsg>(o => NewStreamListenerActorMsgHandler(o));
            Receive<StreamListenerActorStateChangedMsg>(o => StreamListenerActorStateChangedMsgHandler(o));
            Receive<LogPublishedFixturesCountsMsg>(o => LogPublishedFixturesCountsMsgHandler(o));
            Receive<RegisterSdkErrorActorMessage>(a => RegisterSdkErrorActor());
            Receive<SdkErrorMessage>(a => FaultControllerActorOnErrorOcured(a));
            Receive<PathMessage>(a => { _logger.Info("PathMessage delivered"); });


            Context.System.Scheduler.ScheduleTellRepeatedly(new TimeSpan(0, 1, 0), new TimeSpan(0, 1, 0),
                Self, new RegisterSdkErrorActorMessage(), Self);

        }

        private void RegisterSdkErrorActor()
        {
            _logger.Info($"Sending registering message to FaultControllerActor");
            if (SdkActorSystem.FaultControllerActorRef != null)
                SdkActorSystem.FaultControllerActorRef.Tell(new PathMessage() { Path = Self.Path.Address.ToString()} , Self);
        }

        #endregion

        #region Message Handlers

        private void ProcessResourceMsgHandler(ProcessResourceMsg msg)
        {
            _logger.Info(
                $"ProcessResourceMsgHandler for {msg.Resource}");
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.Resource.Id));
            if (streamListenerActor.IsNobody())
            {
                _logger.Info(
                    $"Stream listener for {msg.Resource} doesn't exist. Going to trigger creation.");
                _streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = msg.Resource });
            }
            else
            {
                _logger.Info(
                    $"Stream listener for {msg.Resource} already exists. Going to trigger stream health check.");
                streamListenerActor.Tell(new StreamHealthCheckMsg { Resource = msg.Resource });
            }
        }

        private void StreamListenerInitializedMsgHandler(StreamListenerInitializedMsg msg)
        {
            _logger.Info(
                $"Stream Listener for {msg.Resource} has been Initialized");

            _streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg
            {
                FixtureId = msg.Resource.Id,
                FixtureStatus = msg.Resource.MatchStatus.ToString()
            });
        }

        private void StreamListenerCreationCancelledMsgHandler(StreamListenerCreationCancelledMsg msg)
        {
            _logger.Info(
                $"Stream Listener Initialization for {msg.FixtureId} has been cancelled");

            _streamListenerBuilderActorRef.Tell(msg);
        }

        private void StreamListenerCreationFailedMsgHandler(StreamListenerCreationFailedMsg msg)
        {
            _logger.Error(
                $"Stream Listener for Fixture with fixtureId={msg.FixtureId} Errored - Exception -> {msg.Exception}");

            _streamListenerBuilderActorRef.Tell(msg);
        }

        private void StreamConnectedMsgHandler(StreamConnectedMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            Context.Watch(streamListenerActor);

            _streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg
            {
                FixtureId = msg.FixtureId,
                FixtureStatus = msg.FixtureStatus
            });
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            StopStreamListenerChildActor(msg.FixtureId);
        }

        private void StreamListenerStoppedMsgHandler(StreamListenerStoppedMsg msg)
        {
            StopStreamListenerChildActor(msg.FixtureId);
        }

        private void TerminatedHandler(Terminated t)
        {
            Context.Unwatch(t.ActorRef);

            _logger.Info($"{t.ActorRef.Path.Name} got terminated");

            if (!_shouldSendProcessSportsMessage)
                return;

            var sportsProcessorActor = Context.System.ActorSelection(SportsProcessorActor.Path);
            sportsProcessorActor.Tell(new ProcessSportsMsg());

            //avoid sending ProcessSportsMsg too many times when disconnection occurs
            Context.System.Scheduler.ScheduleTellOnce(
                TimeSpan.FromMilliseconds(_settings.FixtureCheckerFrequency),
                Self,
                new ResetSendProcessSportsMsg(),
                Self);
            _shouldSendProcessSportsMessage = false;
        }

        private void ResetSendProcessSportsMsgHandler(ResetSendProcessSportsMsg msg)
        {
            _shouldSendProcessSportsMessage = true;
        }

        private void RetrieveAndProcessSnapshotMsgHandler(RetrieveAndProcessSnapshotMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            streamListenerActor.Tell(msg);
        }

        private void RestartStreamListenerMsgHandler(RestartStreamListenerMsg msg)
        {
            _logger.Info($"Restarting {Context.Self.Path.Name}");

            _shouldSendProcessSportsMessage = true;
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            streamListenerActor.Tell(new StopStreamingMsg());
        }

        private void ClearFixtureStateMsgHandler(ClearFixtureStateMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            streamListenerActor.Tell(msg);
        }

        private void NewStreamListenerActorMsgHandler(NewStreamListenerActorMsg msg)
        {
            SetStreamListenerState(msg.Sport, msg.FixtureId, StreamListenerState.Initializing);
        }

        private void StreamListenerActorStateChangedMsgHandler(StreamListenerActorStateChangedMsg msg)
        {
            SetStreamListenerState(msg.Sport, msg.FixtureId, msg.NewState);
        }

        private void LogPublishedFixturesCountsMsgHandler(LogPublishedFixturesCountsMsg msg)
        {
            var publishedFixturesTotalCount = _streamListeners.Count > 0
                ? _streamListeners.Keys.Select(sport => _streamListeners[sport].Count).Sum()
                : 0;

            _logger.Info($"PublishedFixturesTotalCount={publishedFixturesTotalCount}");

            var streamListenerStates = Enum.GetValues(typeof(StreamListenerState)).Cast<StreamListenerState>();

            if (publishedFixturesTotalCount > 0)
            {
                foreach (var state in streamListenerStates)
                {
                    var publishedFixturesPerStateCount = _streamListeners.Keys
                        .Select(sport => _streamListeners[sport].Count(s => s.Value.Equals(state)))
                        .Sum();

                    _logger.Info(
                        $"PublishedFixturesPerStateCount={publishedFixturesPerStateCount} having StreamListenerState={state}");

                    foreach (var sport in _streamListeners.Keys)
                    {
                        var publishedFixturesPerStateForSportCount =
                            _streamListeners[sport].Count(s => s.Value.Equals(state));

                        _logger.Info(
                            $"PublishedFixturesPerStateForSportCount={publishedFixturesPerStateForSportCount} having StreamListenerState={state} for Sport={sport}");
                    }
                }
            }
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

        private void FaultControllerActorOnErrorOcured(SdkErrorMessage sdkErrorArgs)
        {
            if (sdkErrorArgs.ShouldSuspend)
            {
                _logger.Warn($"SDK Error occured, all Fixtures will be suspended {sdkErrorArgs.ErrorMessage}");

                var streamListeners = _streamListeners.Values.SelectMany(_ => _.Keys);

                foreach (var sl in streamListeners)
                {
                    IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(sl));
                    streamListenerActor.Tell(new SuspendMessage(SuspensionReason.SDK_ERROR));
                }
            }
        }

        private void StopStreamListenerChildActor(string fixtureId)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(fixtureId));
            if (!streamListenerActor.IsNobody())
            {
                Context.Stop(streamListenerActor);
            }
        }

        private void SetStreamListenerState(string sport, string fixtureId, StreamListenerState state)
        {
            var invalidSport = string.IsNullOrWhiteSpace(sport);
            var invalidFixtureId = string.IsNullOrWhiteSpace(fixtureId);
            if (invalidSport || invalidFixtureId)
            {
                if (invalidSport)
                    _logger.Warn("SetStreamListenerState has sport=null");
                if (invalidFixtureId)
                    _logger.Warn("SetStreamListenerState has fixtureId=null");
                return;
            }

            if (!_streamListeners.ContainsKey(sport))
                _streamListeners.Add(sport, new Dictionary<string, StreamListenerState>());

            if (!_streamListeners[sport].ContainsKey(fixtureId))
                _streamListeners[sport].Add(fixtureId, state);

            _streamListeners[sport][fixtureId] = state;
        }

        #endregion

        #region Private messages

        private class ResetSendProcessSportsMsg
        {
        }

        private class LogPublishedFixturesCountsMsg
        {
        }

        #endregion

    }
}

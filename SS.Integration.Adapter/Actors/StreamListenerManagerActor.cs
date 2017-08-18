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
using SS.Integration.Adapter.Model.Interfaces;

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

            Receive<ProcessResourceMsg>(o => ProcessResourceMsgHandler(o));
            Receive<StreamConnectedMsg>(o => StreamConnectedMsgHandler(o));
            Receive<StreamDisconnectedMsg>(o => StreamDisconnectedMsgHandler(o));
            Receive<StreamListenerStoppedMsg>(o => StreamListenerStoppedMsgHandler(o));
            Receive<StartStreamingNotRespondingMsg>(o => StopStreamListenerChildActor(o.FixtureId));
            Receive<StreamListenerInitializedMsg>(o => StreamListenerInitializedMsgHandler(o));
            Receive<StreamListenerCreationFailedMsg>(o => StreamListenerCreationFailedMsgHandler(o));
            Receive<Terminated>(o => TerminatedHandler(o));
            Receive<ResetSendProcessSportsMsg>(o => ResetSendProcessSportsMsgHandler(o));
            Receive<RetrieveAndProcessSnapshotMsg>(o => RetrieveAndProcessSnapshotMsgHandler(o));
            Receive<RestartStreamListenerMsg>(o => RestartStreamListenerMsgHandler(o));
            Receive<ClearFixtureStateMsg>(o => ClearFixtureStateMsgHandler(o));
        }

        #endregion

        #region Message Handlers

        private void ProcessResourceMsgHandler(ProcessResourceMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.Resource.Id));
            if (streamListenerActor.IsNobody())
            {
                _streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = msg.Resource });
            }
            else
            {
                streamListenerActor.Tell(new StreamHealthCheckMsg { Resource = msg.Resource });
            }
        }

        private void StreamListenerInitializedMsgHandler(StreamListenerInitializedMsg msg)
        {
            _logger.Info(
                $"Stream Listener for {msg.Resource} has been Initialized");

            _streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg { FixtureId = msg.Resource.Id });
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

            _streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg { FixtureId = msg.FixtureId });
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

        #endregion

        #region Private methods

        private void StopStreamListenerChildActor(string fixtureId)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(fixtureId));
            if (!streamListenerActor.IsNobody())
            {
                Context.Stop(streamListenerActor);
            }
        }

        #endregion

        #region Private messages

        private class ResetSendProcessSportsMsg
        {
        }

        #endregion

    }
}

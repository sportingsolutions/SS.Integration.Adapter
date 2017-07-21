using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Dispatch.SysMsg;
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

        #region Private members

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerManagerActor).ToString());
        private readonly ISettings _settings;
        private readonly IActorRef _streamListenerBuilderActorRef;
        private bool _shouldSendProcessSportsMessage;

        #endregion

        #region Constructors

        public StreamListenerManagerActor(
            ISettings settings,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            IStreamValidation streamValidation,
            IFixtureValidation fixtureValidation)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (adapterPlugin == null)
                throw new ArgumentNullException(nameof(adapterPlugin));
            if (stateManager == null)
                throw new ArgumentNullException(nameof(stateManager));

            _shouldSendProcessSportsMessage = true;

            _streamListenerBuilderActorRef =
                Context.ActorOf(Props.Create(() =>
                        new StreamListenerBuilderActor(
                            settings,
                            Context,
                            adapterPlugin,
                            stateManager,
                            streamValidation,
                            fixtureValidation)),
                    StreamListenerBuilderActor.ActorName);

            Receive<ProcessResourceMsg>(o => ProcessResourceMsgHandler(o));
            Receive<StreamConnectedMsg>(o => StreamConnectedMsgHandler(o));
            Receive<StreamDisconnectedMsg>(o => StreamDisconnectedMsgHandler(o));
            Receive<StreamListenerStoppedMsg>(o => StreamListenerStoppedMsgHandler(o));
            Receive<StartStreamingNotRespondingMsg>(o => StopStreamListenerChildActor(o.FixtureId));
            Receive<StreamListenerCreationCompletedMsg>(o => StreamListenerCreationCompletedMsgHandler(o));
            Receive<StreamListenerCreationFailedMsg>(o => StreamListenerCreationFailedMsgHandler(o));
            Receive<Terminated>(o => TerminatedHandler(o));
            Receive<ResetSendProcessSportsMsg>(o => ResetSendProcessSportsMsgHandler(o));
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

        private void StreamListenerCreationCompletedMsgHandler(StreamListenerCreationCompletedMsg msg)
        {
            _logger.Info(
                $"Stream Listener has been created for Resource {msg.Resource}");
        }

        private void StreamListenerCreationFailedMsgHandler(StreamListenerCreationFailedMsg msg)
        {
            _logger.Error(
                $"Stream Listener Creation Failed for Resource {msg.Resource} - Exception -> {msg.Exception}");
        }

        private void StreamConnectedMsgHandler(StreamConnectedMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            Context.Watch(streamListenerActor);
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

            if (!_shouldSendProcessSportsMessage)
                return;

            var sportsProcessorActor = Context.System.ActorSelection(SportsProcessorActor.Path);
            sportsProcessorActor.Tell(new ProcessSportsMessage());

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

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

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerManagerActor).ToString());
        private readonly ISettings _settings;
        private readonly IEventState _eventState;
        private readonly IActorRef _streamListenerBuilderActorRef;
        private readonly Dictionary<long, string> _sportProcessingTrigger;

        #endregion

        #region Constructors

        public StreamListenerManagerActor(
            ISettings settings,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            IEventState eventState,
            IStreamValidation streamValidation,
            IFixtureValidation fixtureValidation)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (adapterPlugin == null)
                throw new ArgumentNullException(nameof(adapterPlugin));
            if (stateManager == null)
                throw new ArgumentNullException(nameof(stateManager));
            _eventState = eventState ?? throw new ArgumentNullException(nameof(eventState));

            _sportProcessingTrigger = new Dictionary<long, string>();

            _streamListenerBuilderActorRef =
                Context.ActorOf(Props.Create(() =>
                        new StreamListenerBuilderActor(
                            settings,
                            Context,
                            adapterPlugin,
                            eventState,
                            stateManager,
                            streamValidation,
                            fixtureValidation)),
                    StreamListenerBuilderActor.ActorName);

            Receive<ProcessResourceMsg>(o => ProcessResourceMsgHandler(o));
            Receive<StreamConnectedMsg>(o => StreamConnectedMsgHandler(o));
            Receive<StreamDisconnectedMsg>(o => StreamDisconnectedMsgHandler(o));
            Receive<StreamListenerStoppedMsg>(o => StreamListenerStoppedMsgHandler(o));
            Receive<StartStreamingNotRespondingMsg>(o => StopStreamListenerChildActor(o.FixtureId));
            Receive<Terminated>(o => TerminatedHandler(o));
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

        private void StreamConnectedMsgHandler(StreamConnectedMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            Context.Watch(streamListenerActor);
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(msg.FixtureId));
            _sportProcessingTrigger[streamListenerActor.Path.Uid] = msg.Sport;

            StopStreamListenerChildActor(msg.FixtureId);
        }

        private void StreamListenerStoppedMsgHandler(StreamListenerStoppedMsg msg)
        {
            StopStreamListenerChildActor(msg.FixtureId);
        }

        private void StopStreamListenerChildActor(string fixtureId)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.GetName(fixtureId));
            if (!streamListenerActor.IsNobody())
            {
                Context.Stop(streamListenerActor);
            }
        }

        private void TerminatedHandler(Terminated t)
        {
            Context.Unwatch(t.ActorRef);

            if (_sportProcessingTrigger.ContainsKey(t.ActorRef.Path.Uid))
            {
                var sport = _sportProcessingTrigger[t.ActorRef.Path.Uid];
                var sportProcessorRouterActor = Context.System.ActorSelection(SportProcessorRouterActor.Path);
                sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = sport });
                _sportProcessingTrigger.Remove(t.ActorRef.Path.Uid);
            }
        }

        #endregion
    }
}

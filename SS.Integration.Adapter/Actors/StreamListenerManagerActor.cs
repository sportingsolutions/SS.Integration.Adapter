using System;
using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    //This actor manages all StreamListeners 
    public class StreamListenerManagerActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(StreamListenerManagerActor);

        #endregion

        #region Attributes

        private readonly ISettings _settings;
        private readonly IActorRef _streamListenerBuilderActorRef;
        private bool _shouldSendProcessSportMessage;

        #endregion

        #region Constructors

        public StreamListenerManagerActor(
            ISettings settings,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (adapterPlugin == null)
                throw new ArgumentNullException(nameof(adapterPlugin));
            if (stateManager == null)
                throw new ArgumentNullException(nameof(stateManager));

            IEventState eventState = EventState.Create(new FileStoreProvider(settings.StateProviderPath), settings);

            _shouldSendProcessSportMessage = true;

            _streamListenerBuilderActorRef =
                Context.ActorOf(Props.Create(() =>
                        new StreamListenerBuilderActor(
                            settings,
                            Context,
                            adapterPlugin,
                            eventState,
                            stateManager)),
                    StreamListenerBuilderActor.ActorName);

            Receive<CreateStreamListenerMsg>(o => CreateStreamListenerMsgHandler(o));
            Receive<StreamDisconnectedMsg>(o => StreamDisconnectedMsgHandler(o));
            Receive<StreamListenerStoppedMsg>(o => StreamListenerStoppedMsgHandler(o));
            Receive<ResetSendProcessSportMsg>(o => ResetSendProcessSportMsgHandler(o));
        }

        #endregion

        #region Private methods

        private void CreateStreamListenerMsgHandler(CreateStreamListenerMsg msg)
        {
            if (Equals(Context.Child(StreamListenerActor.ActorName + "For" + msg.Resource.Id), Nobody.Instance))
            {
                _streamListenerBuilderActorRef.Tell(msg);
            }
        }

        private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        {
            StopStreamListenerChildActor(msg.FixtureId);

            if (_shouldSendProcessSportMessage)
            {
                Context.Parent.Tell(new ProcessSportMsg {Sport = msg.Sport});

                //should not send ProcessSportMsg during in the same cycle
                Context.System.Scheduler.ScheduleTellOnce(
                    TimeSpan.FromMilliseconds(_settings.FixtureCheckerFrequency),
                    Self,
                    new ResetSendProcessSportMsg(),
                    Self);
                _shouldSendProcessSportMessage = false;
            }
        }

        private void StreamListenerStoppedMsgHandler(StreamListenerStoppedMsg msg)
        {
            StopStreamListenerChildActor(msg.FixtureId);
        }

        private void StopStreamListenerChildActor(string fixtureId)
        {
            IActorRef streamListenerActor = Context.Child(StreamListenerActor.ActorName + "For" + fixtureId);
            if (!Equals(streamListenerActor, Nobody.Instance))
                Context.Stop(streamListenerActor);
        }

        private void ResetSendProcessSportMsgHandler(ResetSendProcessSportMsg msg)
        {
            _shouldSendProcessSportMessage = true;
        }

        #endregion

        #region Private messages

        private class ResetSendProcessSportMsg
        {   
        }

        #endregion
    }
}

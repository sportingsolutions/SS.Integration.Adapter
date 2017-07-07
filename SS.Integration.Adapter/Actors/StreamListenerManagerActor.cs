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

        #endregion

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerManagerActor).ToString());
        private readonly ISettings _settings;
        private readonly IEventState _eventState;
        private readonly IActorRef _streamListenerBuilderActorRef;
        private bool _shouldSendProcessSportMessage;

        #endregion

        #region Constructors

        public StreamListenerManagerActor(
            ISettings settings,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            IEventState eventState)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (adapterPlugin == null)
                throw new ArgumentNullException(nameof(adapterPlugin));
            if (stateManager == null)
                throw new ArgumentNullException(nameof(stateManager));
            _eventState = eventState ?? throw new ArgumentNullException(nameof(eventState));

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
            Receive<StreamConnectedMsg>(o => StreamConnectedMsgHandler(o));
            Receive<StreamDisconnectedMsg>(o => StreamDisconnectedMsgHandler(o));
            Receive<StreamListenerStoppedMsg>(o => StreamListenerStoppedMsgHandler(o));
            Receive<StartStreamingNotRespondingMsg>(o => StopStreamListenerChildActor(o.FixtureId));
            Receive<ResetSendProcessSportMsg>(o => ResetSendProcessSportMsgHandler(o));
        }

        #endregion

        #region Private methods

        private void CreateStreamListenerMsgHandler(CreateStreamListenerMsg msg)
        {
            if (Context.Child(StreamListenerActor.ActorName + "For" + msg.Resource.Id).IsNobody())
            {
                _streamListenerBuilderActorRef.Tell(msg);
            }
        }

        private void StreamConnectedMsgHandler(StreamConnectedMsg msg)
        {
            SaveEventState();
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
            if (!streamListenerActor.IsNobody())
                streamListenerActor.GracefulStop(TimeSpan.FromSeconds(5)).Wait();
        }

        private void ResetSendProcessSportMsgHandler(ResetSendProcessSportMsg msg)
        {
            _shouldSendProcessSportMessage = true;
        }

        #endregion

        #region Private methods

        private void SaveEventState()
        {
            try
            {
                _eventState.WriteToFile();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Event state errored on attempt to save it: {0}", ex);
            }
        }

        #endregion

        #region Private messages

        private class ResetSendProcessSportMsg
        {   
        }

        #endregion
    }
}

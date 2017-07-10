using System;
using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// StreamListener Builder is responsible for mananging concurrent creation of stream listeners
    /// </summary>
    public class StreamListenerBuilderActor : ReceiveActor, IWithUnboundedStash
    {
        #region Constants

        public const string ActorName = nameof(StreamListenerBuilderActor);

        #endregion

        #region Attributes

        private readonly ISettings _settings;
        private readonly IActorContext _streamListenerManagerActorContext;
        private readonly IAdapterPlugin _adapterPlugin;
        private readonly IEventState _eventState;
        private readonly IStateManager _stateManager;
        private static int _concurrentInitializations = 0;

        #endregion

        #region Properties

        public IStash Stash { get; set; }

        #endregion

        #region Constructors

        public StreamListenerBuilderActor(
            ISettings settings,
            IActorContext streamListenerManagerActorContext,
            IAdapterPlugin adapterPlugin,
            IEventState eventState,
            IStateManager stateManager)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _streamListenerManagerActorContext =
                streamListenerManagerActorContext ??
                throw new ArgumentNullException(nameof(streamListenerManagerActorContext));
            _adapterPlugin = adapterPlugin ?? throw new ArgumentNullException(nameof(adapterPlugin));
            _eventState = eventState ?? throw new ArgumentNullException(nameof(eventState));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));

            Active();
        }

        #endregion

        #region Behaviors

        //In the active state StreamListeners can be created on demand
        private void Active()
        {
            Receive<CreateStreamListenerMsg>(o => CreateStreamListenerMsgHandler(o));
            Receive<BuildStreamListenerActorMsg>(o => BuildStreamListenerActorMsgHandler(o));
        }

        //In the busy state the maximum concurrency has been already used and creation needs to be postponed until later
        private void Busy()
        {
            //Stash messages until CreationCompleted/Failed message is received
            Receive<CreateStreamListenerMsg>(o =>
            {
                if (_concurrentInitializations > _settings.FixtureCreationConcurrency)
                {
                    Stash.Stash();
                }
                else
                {
                    Become(Active);
                    Stash.Unstash();
                }
            });
        }

        #endregion

        #region Private methods

        private void CreateStreamListenerMsgHandler(CreateStreamListenerMsg msg)
        {
            _concurrentInitializations++;

            if (_concurrentInitializations > _settings.FixtureCreationConcurrency)
            {
                Become(Busy);
            }
            else
            {
                Self.Tell(new BuildStreamListenerActorMsg { Resource = msg.Resource });
            }
        }

        private void BuildStreamListenerActorMsgHandler(BuildStreamListenerActorMsg msg)
        {
            try
            {
                _streamListenerManagerActorContext.ActorOf(Props.Create(() =>
                        new StreamListenerActor(
                            msg.Resource,
                            _adapterPlugin,
                            _eventState,
                            _stateManager,
                            _settings)),
                    StreamListenerActor.GetName(msg.Resource.Id));
            }
            finally
            {
                _concurrentInitializations--;
            }
        }

        #endregion

        #region Private messages

        private class BuildStreamListenerActorMsg
        {
            public IResourceFacade Resource { get; set; }
        }

        #endregion
    }
}

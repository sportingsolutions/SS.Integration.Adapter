using Akka.Actor;
using Akka.Routing;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    public static class AdapterActorSystem
    {
        private static ActorSystem _actorSystem;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="udApiService"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        public static void Init(
            ISettings settings,
            IServiceFacade udApiService,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager)
        {
            _actorSystem = ActorSystem.Create("AdapterSystem");

            IEventState eventState = EventState.Create(new FileStoreProvider(settings.StateProviderPath), settings);

            ActorSystem.ActorOf(
                Props.Create(() =>
                    new StreamListenerManagerActor(
                        settings,
                        adapterPlugin,
                        stateManager,
                        eventState)),
                StreamListenerManagerActor.ActorName);

            var sportProcessorRouterActor = ActorSystem.ActorOf(
                Props.Create(() =>
                        new SportProcessorRouterActor(
                            settings,
                            adapterPlugin,
                            stateManager,
                            eventState,
                            udApiService))
                    .WithRouter(new SmallestMailboxPool(settings.FixtureCreationConcurrency)),
                SportProcessorRouterActor.ActorName);

            ActorSystem.ActorOf(
                Props.Create(() =>
                    new SportsProcessorActor(
                        settings,
                        udApiService,
                        sportProcessorRouterActor)),
                SportsProcessorActor.ActorName);
        }

        public static void Dispose()
        {
            _actorSystem?.Dispose();
            _actorSystem = null;
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}

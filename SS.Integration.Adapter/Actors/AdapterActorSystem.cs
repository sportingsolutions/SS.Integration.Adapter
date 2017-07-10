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
        /// Actor system shouldn't be provided unless your implemention specifically requires it
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="udApiService"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        /// <param name="actorSystem"></param>
        public static void Init(
            ISettings settings,
            IServiceFacade udApiService,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            ActorSystem actorSystem = null)
        {
            _actorSystem = actorSystem ?? ActorSystem.Create("AdapterSystem");

            IEventState eventState = EventState.Create(new FileStoreProvider(settings.StateProviderPath), settings);

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

            ActorSystem.ActorOf(
                Props.Create(() =>
                    new StreamListenerManagerActor(
                        settings,
                        adapterPlugin,
                        stateManager,
                        eventState)),
                StreamListenerManagerActor.ActorName);
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}

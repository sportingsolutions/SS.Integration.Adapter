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
        private static IActorRef _sportsProcessorActor;
        private static IActorRef _sportProcessorRouterActor;
        private static IActorRef _streamListenerManagerActor;
        private static IActorRef _fixtureStateActor;

        public static ActorSystem ActorSystem => _actorSystem;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="udApiService"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        /// <param name="streamValidation"></param>
        /// <param name="fixtureValidation"></param>
        public static void Init(
            ISettings settings,
            IServiceFacade udApiService,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            IStreamValidation streamValidation,
            IFixtureValidation fixtureValidation)
        {
            _actorSystem = ActorSystem.Create("AdapterSystem");

            var fileStoreProvider = new FileStoreProvider(settings.StateProviderPath);
            _fixtureStateActor = ActorSystem.ActorOf(
                Props.Create(() =>
                    new FixtureStateActor(
                        settings,
                        fileStoreProvider)),
                FixtureStateActor.ActorName);

            _streamListenerManagerActor = ActorSystem.ActorOf(
                Props.Create(() =>
                    new StreamListenerManagerActor(
                        settings,
                        adapterPlugin,
                        stateManager,
                        streamValidation,
                        fixtureValidation)),
                StreamListenerManagerActor.ActorName);

            _sportProcessorRouterActor = ActorSystem.ActorOf(
                Props.Create(() => new SportProcessorRouterActor(udApiService))
                    .WithRouter(new SmallestMailboxPool(settings.FixtureCreationConcurrency)),
                SportProcessorRouterActor.ActorName);

            _sportsProcessorActor = ActorSystem.ActorOf(
                Props.Create(() =>
                    new SportsProcessorActor(
                        settings,
                        udApiService,
                        _sportProcessorRouterActor)),
                SportsProcessorActor.ActorName);
        }

        public static void Dispose()
        {
            _actorSystem?.Stop(_sportsProcessorActor);
            _actorSystem?.Stop(_sportProcessorRouterActor);
            _actorSystem?.Stop(_streamListenerManagerActor);
            _actorSystem?.Stop(_fixtureStateActor);
            _actorSystem?.Dispose();
            _actorSystem = null;
        }
    }
}

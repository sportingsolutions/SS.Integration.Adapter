using System;
using Akka.Actor;
using Akka.Routing;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    public static class AdapterActorSystem
    {
        private const string UserSystemPath = "/user/";

        public const string SportsProcessorActorPath = UserSystemPath + SportsProcessorActor.ActorName;
        public const string SportProcessorRouterActorPath = UserSystemPath + SportProcessorRouterActor.ActorName;
        public const string StreamListenerManagerActorPath = UserSystemPath + StreamListenerManagerActor.ActorName;

        private static ActorSystem _actorSystem = ActorSystem.Create("AdapterSystem");
        private static ISettings _settings;
        private static IServiceFacade _udApiService;
        private static IAdapterPlugin _adapterPlugin;
        private static IStateManager _stateManager;

        static AdapterActorSystem()
        {
        }

        /// <summary>
        /// Actor system shouldn't be provided unless your implemention specifically requires it
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="udApiService"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        /// <param name="actorSystem"></param>
        /// <param name="initialiseActors"></param>
        public static void Init(
            ISettings settings,
            IServiceFacade udApiService,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            ActorSystem actorSystem = null,
            bool initialiseActors = true)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _udApiService = udApiService ?? throw new ArgumentNullException(nameof(udApiService));
            _adapterPlugin = adapterPlugin ?? throw new ArgumentNullException(nameof(adapterPlugin));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _actorSystem = actorSystem ?? _actorSystem;

            if (initialiseActors)
            {
                var sportProcessorRouterActor = ActorSystem.ActorOf(
                    Props.Create(() =>
                            new SportProcessorRouterActor(
                                _settings,
                                _adapterPlugin,
                                _stateManager,
                                _udApiService))
                        .WithRouter(new SmallestMailboxPool(_settings.FixtureCreationConcurrency)),
                    "sport-processor-pool");

                ActorSystem.ActorOf(
                    Props.Create(() =>
                        new SportsProcessorActor(
                            _settings,
                            _udApiService,
                            sportProcessorRouterActor)),
                    SportsProcessorActor.ActorName);
            }
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}

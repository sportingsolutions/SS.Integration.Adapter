using Akka.Actor;
using Akka.Routing;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    public static class AdapterActorSystem
    {
        private const string UserSystemPath = "/user/";

        public const string FixtureManagerActorPath = UserSystemPath + FixtureManagerActor.ActorName;
        public const string SportsProcessorRouterActorPath = UserSystemPath + FixtureManagerActor.ActorName;

        private static ActorSystem _actorSystem = ActorSystem.Create("AdapterSystem");
        private static ISettings _settings;
        private static IServiceFacade _udApiService;
        private static IStreamListenerManager _streamListenerManager;

        static AdapterActorSystem()
        {
        }

        /// <summary>
        /// Actor system shouldn't be provided unless your implemention specifically requires it
        /// </summary>
        /// <param name="actorSystem"></param>
        /// <param name="initialiseActors"></param>
        public static void Init(ISettings settings, IServiceFacade udAPIService, IStreamListenerManager streamListenerManager, ActorSystem actorSystem = null, bool initialiseActors = true)
        {
            _settings = settings;
            _udApiService = udAPIService;
            _streamListenerManager = streamListenerManager;
            _actorSystem = actorSystem ?? _actorSystem;

            if (initialiseActors)
            {
                var sportProcessorRouterActor = ActorSystem.ActorOf(
                    Props.Create<SportProcessorActor>(_streamListenerManager)
                        .WithRouter(new SmallestMailboxPool(_settings.FixtureCreationConcurrency)),
                    "sport-processor-pool");

                ActorSystem.ActorOf(
                    Props.Create(() =>
                        new FixtureManagerActor(
                            _settings,
                            _udApiService,
                            sportProcessorRouterActor)),
                    FixtureManagerActor.ActorName);
            }
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}

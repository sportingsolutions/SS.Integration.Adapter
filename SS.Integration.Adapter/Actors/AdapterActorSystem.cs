using Akka.Actor;
using Akka.Routing;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    public static class AdapterActorSystem
    {
        private const string UserSystemPath = "/user/";

        public const string SportsProcessorActorPath = UserSystemPath + SportsProcessorActor.ActorName;
        public const string SportProcessorActorPath = UserSystemPath + SportProcessorRouterActor.ActorName;

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
        /// <param name="streamListenerManager"></param>
        /// <param name="actorSystem"></param>
        /// <param name="initialiseActors"></param>
        /// <param name="settings"></param>
        /// <param name="udApiService"></param>
        public static void Init(
            ISettings settings, 
            IServiceFacade udApiService, 
            IStreamListenerManager streamListenerManager, 
            ActorSystem actorSystem = null, 
            bool initialiseActors = true)
        {
            _settings = settings;
            _udApiService = udApiService;
            _streamListenerManager = streamListenerManager;
            _actorSystem = actorSystem ?? _actorSystem;

            if (initialiseActors)
            {
                var sportProcessorRouterActor = ActorSystem.ActorOf(
                    Props.Create<SportProcessorRouterActor>(_streamListenerManager)
                        .WithRouter(new SmallestMailboxPool(_settings.FixtureCreationConcurrency)),
                    "sport-processor-pool");

                ActorSystem.ActorOf(
                    Props.Create(() =>
                        new SportsProcessorActor(
                            _settings,
                            _udApiService,
                            sportProcessorRouterActor,
                            _streamListenerManager)),
                    SportsProcessorActor.ActorName);
            }
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}

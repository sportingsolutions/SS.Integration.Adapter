using Akka.Actor;

namespace SS.Integration.Adapter
{
    public class AdapterActorSystem
    {
        private static ActorSystem _actorSystem = ActorSystem.Create("AdapterSystem");

        private const string UserSystemPath = "/user/";

        public const string FixtureManagerActorPath = UserSystemPath + FixtureManagerActor.ActorName;

        static AdapterActorSystem()
        {

        }

        /// <summary>
        /// Actor system shouldn't be provided unless your implemention specifically requires it
        /// </summary>
        /// <param name="actorSystem"></param>
        /// <param name="initialiseActors"></param>
        public static void Init(ActorSystem actorSystem = null, bool initialiseActors = true)
        {
            _actorSystem = actorSystem ?? _actorSystem;

            if (initialiseActors)
            {
                ActorSystem.ActorOf(Props.Create(() => new FixtureManagerActor()), EchoControllerActor.ActorName);
            }
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}

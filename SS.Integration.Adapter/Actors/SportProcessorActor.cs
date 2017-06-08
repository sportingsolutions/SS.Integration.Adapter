using Akka.Actor;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    public class SportProcessorActor : ReceiveActor, IWithUnboundedStash
    {
        public const string ActorName = "FixtureManagerActor";

        private readonly IStreamListenerManager _streamListenerManager;

        public IStash Stash { get; set; }

        public SportProcessorActor(
            ISettings settings,
            IServiceFacade serviceFacade)
        {
            
        }
    }
}

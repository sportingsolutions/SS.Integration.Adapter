using Akka.Actor;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    public class SportProcessorRouterActor : ReceiveActor, IWithUnboundedStash
    {
        public const string ActorName = "SportProcessorRouterActor";

        private readonly ISettings _settings;
        private readonly IServiceFacade _serviceFacade;
        private readonly IStreamListenerManager _streamListenerManager;

        public IStash Stash { get; set; }

        public SportProcessorRouterActor(
            ISettings settings,
            IServiceFacade serviceFacade,
            IStreamListenerManager streamListenerManager)
        {
            _settings = settings;
            _serviceFacade = serviceFacade;
            _streamListenerManager = streamListenerManager;

            Receive<ProcessSportMessage>(o => ProcessSportMessageHandler(o));
        }

        private void ProcessSportMessageHandler(ProcessSportMessage processSportMessage)
        {
            throw new System.NotImplementedException();
        }
    }
}

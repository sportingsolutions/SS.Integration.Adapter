using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    //This actor manages all StreamListeners 
    public class StreamListenerManagerActor : ReceiveActor
    {
        public const string ActorName = "StreamListenerManagerActor";

        private readonly IActorRef _streamListenerBuilderActorRef;

        public StreamListenerManagerActor(ISettings settings)
        {
            _streamListenerBuilderActorRef = Context.ActorOf(Props.Create(() => new StreamListenerBuilderActor(settings)));

            DefaultBehaviour();
        }

        private void DefaultBehaviour()
        {
            Receive<CreateStreamListenerMessage>(o => CreateStreamListenerMessageHandler(o));
        }

        private void CreateStreamListenerMessageHandler(CreateStreamListenerMessage message)
        {
            if (Equals(Context.Child(message.Resource.Id), Nobody.Instance))
            {
                _streamListenerBuilderActorRef.Tell(message);
            }
        }
    }
}

using System.Collections.Generic;
using Akka.Actor;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    //This actor manages all StreamListeners 
    public class StreamListenerManagerActor : ReceiveActor
    {
        public const string ActorName = "StreamListenerManagerActor";

        private IActorRef _streamListenerBuilderActorRef;

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

        // Receive<CreateStreamListenerMessage> -> Validate whether Stream Listener exists and if not passes it on to StreamListenerBuilderActor
        // Use Resource current details to further validate StreamListener (same as now)

        // Receive<StreamDisconnected> -> Removes StreamListener and recreates a new one using a NEW resource
        // Receive<FixtureCompletedMsg> -> Removes StreamListener and cleans up
        // Receive<StreamListenerCreationCompleted> -> Remove the current one and recreate with a new Resource
        // Receive<StreamListenerCreationFailed> -> Remove the current one and recreate with a new Resource
    }
}

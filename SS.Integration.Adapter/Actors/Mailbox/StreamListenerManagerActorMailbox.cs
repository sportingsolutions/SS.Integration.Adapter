using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using log4net;

namespace SS.Integration.Adapter.Actors.Mailbox
{
    public class StreamListenerManagerActorMailbox : UnboundedPriorityMailbox
    {
        public StreamListenerManagerActorMailbox(Settings settings, Config config) : base(settings, config)
        {
        }

        protected override int PriorityGenerator(object message)
        {
            var messageType = message.GetType();
            if (message is StreamListenerManagerActor.LogPublishedFixturesCountsMsg)
            {
                return 0;
            }
            return 1;
        }
    }
}

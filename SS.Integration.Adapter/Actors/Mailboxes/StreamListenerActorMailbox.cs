using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using SS.Integration.Adapter.Actors.Messages;

namespace SS.Integration.Adapter.Actors.Mailboxes
{
    public class StreamListenerActorMailbox : UnboundedPriorityMailbox
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActorMailbox));

        public StreamListenerActorMailbox(Settings settings, Config config) : base(settings, config)
        {
        }

        protected override int PriorityGenerator(object message)
        {
            var messageType = message.GetType();

            int priority = 1;
            if (message is StreamHealthCheckMsg)
            {
                priority = 0;
                _logger.Debug($"PriorityGenerator set priority={priority} with messageType={messageType}");
            }
            
            return priority;
        }

    }
}

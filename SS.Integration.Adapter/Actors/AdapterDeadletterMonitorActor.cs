using Akka.Actor;
using Akka.Event;
using log4net;

namespace SS.Integration.Adapter.Actors
{
    // A dead letter handling actor specifically for messages of type "DeadLetter"
    public class AdapterDeadletterMonitorActor : ReceiveActor
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(AdapterDeadletterMonitorActor));

        public AdapterDeadletterMonitorActor()
        {
            Receive<DeadLetter>(dl => HandleDeadletter(dl));
        }

        private void HandleDeadletter(DeadLetter dl)
        {
            _logger.Warn($"DeadLetter captured: {dl.Message}, sender: {dl.Sender}, recipient: {dl.Recipient}");
        }
    }
}

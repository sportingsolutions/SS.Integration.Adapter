using System;
using System.Collections.Generic;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responibility to process all the sports in parallel using different instances/threads managed by AKKA Router 
    /// A single sport is processed on a separate thread by a single instance acting like a child actor.
    /// The child actor is responsible for triggering the creation/processing of that sport's resources stream listeners to the StreamListenerManagerActor
    /// </summary>
    public class SportProcessorRouterActor : ReceiveActor
    {
        public const string ActorName = "SportProcessorRouterActor";

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportProcessorRouterActor));
        private readonly ISettings _settings;
        private readonly IServiceFacade _serviceFacade;

        public SportProcessorRouterActor(ISettings settings, IServiceFacade serviceFacade)
        {
            _settings = settings;
            _serviceFacade = serviceFacade;
            DefaultBehavior();
        }

        private void DefaultBehavior()
        {
            Receive<ProcessSportMessage>(o => ProcessSportMessageHandler(o));
        }

        private void ProcessSportMessageHandler(ProcessSportMessage message)
        {
            var resources = _serviceFacade.GetResources(message.Sport);
            if (ValidateResources(resources, message.Sport))
            {
                _logger.DebugFormat(
                    $"Received {resources.Count} fixtures to process in sport={message.Sport}");

                if (resources.Count > 1)
                {
                    resources.Sort((x, y) =>
                    {
                        if (x.Content.MatchStatus > y.Content.MatchStatus)
                            return -1;

                        return x.Content.MatchStatus < y.Content.MatchStatus
                            ? 1
                            : DateTime.Parse(x.Content.StartTime).CompareTo(DateTime.Parse(y.Content.StartTime));

                    });
                }

                ICanTell streamListenerManagerActor =
                    Equals(Context.Child(message.Sport), Nobody.Instance)
                        ? Context.ActorOf(Props.Create(() => new StreamListenerManagerActor(_settings)), message.Sport)
                        : Context.ActorSelection($"{Self.Path}/{message.Sport}") as ICanTell;
                foreach (var resource in resources)
                {
                    streamListenerManagerActor.Tell(new CreateStreamListenerMessage { Resource = resource }, Self);
                }
            }
        }

        private bool ValidateResources(IList<IResourceFacade> resources, string sport)
        {
            var valid = true;

            if (resources == null)
            {
                _logger.WarnFormat("Cannot find sport={0} in UDAPI....", sport);
                valid = false;
            }
            else if (resources.Count == 0)
            {
                _logger.DebugFormat("There are currently no fixtures for sport={0} in UDAPI", sport);
                valid = false;
            }

            return valid;
        }
    }
}

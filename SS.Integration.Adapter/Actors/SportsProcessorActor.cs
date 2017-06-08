using System;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responibility to repeatedly schedule all sports processing at specified interval (default 60 seconds)
    /// Also Statistics Generation is triggered with each interval
    /// </summary>
    public class SportsProcessorActor : ReceiveActor, IWithUnboundedStash
    {
        public const string ActorName = "SportsProcessorActor";

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportsProcessorActor));
        private readonly IServiceFacade _serviceFacade;
        private readonly IActorRef _sportProcessorRouterActor;
        private readonly IActorRef _statsActor;

        public IStash Stash { get; set; }

        public SportsProcessorActor(
            ISettings settings,
            IServiceFacade serviceFacade,
            IActorRef sportProcessorRouterActor,
            IStreamListenerManager streamListenerManager)
        {
            _serviceFacade = serviceFacade;
            _sportProcessorRouterActor = sportProcessorRouterActor;

            DefaultBehavior();
        
            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(settings.FixtureCheckerFrequency),
                Self,
                new ProcessSportsMessage(),
                Self);
            _statsActor = Context.ActorOf(Props.Create<StatsActor>(streamListenerManager), StatsActor.ActorName);
        }

        private void DefaultBehavior()
        {
            Receive<ProcessSportsMessage>(o => ProcessSports());
        }

        private void ProcessSports()
        {
            _logger.Debug("Start ProcessSports");

            var sports = _serviceFacade.GetSports();

            foreach (var sport in sports)
            {
                _sportProcessorRouterActor.Tell(new ProcessSportMessage {Sport = sport.Name});
            }

            _statsActor.Tell(new ProcessStatistics());

            _logger.Debug("End ProcessSports");
        }

        private class ProcessSportsMessage
        {
        }
    }
}

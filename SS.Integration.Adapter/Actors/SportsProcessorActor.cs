using System;
using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responibility to repeatedly schedule all sports processing at specified interval (default 60 seconds)
    /// Also Statistics Generation is triggered with each interval
    /// </summary>
    public class SportsProcessorActor : ReceiveActor
    {
        public const string ActorName = "SportsProcessorActor";

        private readonly IServiceFacade _serviceFacade;
        private readonly IActorRef _sportProcessorRouterActor;
        private readonly IActorRef _statsActor;

        public SportsProcessorActor(
            ISettings settings,
            IServiceFacade serviceFacade,
            IActorRef sportProcessorRouterActor)
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
            _statsActor = Context.ActorOf(Props.Create<StatsActor>(), StatsActor.ActorName);
        }

        private void DefaultBehavior()
        {
            Receive<ProcessSportsMessage>(o => ProcessSports());
        }

        private void ProcessSports()
        {
            //_statsActor.Tell(new ProcessStatistics());

            var sports = _serviceFacade.GetSports();

            foreach (var sport in sports)
            {
                _sportProcessorRouterActor.Tell(new ProcessSportMessage {Sport = sport.Name});
            }
        }

        #region Private messages

        private class ProcessSportsMessage
        {
        }

        #endregion
    }
}
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
        #region Constants

        public const string ActorName = nameof(SportsProcessorActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Attribues

        private readonly IServiceFacade _serviceFacade;
        private readonly IActorRef _sportProcessorRouterActor;
        private readonly IActorRef _statsActor;

        #endregion

        #region Constructors

        public SportsProcessorActor(
            ISettings settings,
            IServiceFacade serviceFacade,
            IActorRef sportProcessorRouterActor)
        {
            _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));
            _sportProcessorRouterActor = sportProcessorRouterActor ?? throw new ArgumentNullException(nameof(sportProcessorRouterActor));

            Receive<ProcessSportsMessage>(o => ProcessSports());

            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(settings.FixtureCheckerFrequency),
                Self,
                new ProcessSportsMessage(),
                Self);
        }

        #endregion

        #region Private methods

        private void ProcessSports()
        {
            var sports = _serviceFacade.GetSports();

            foreach (var sport in sports)
            {
                _sportProcessorRouterActor.Tell(new ProcessSportMsg {Sport = sport.Name});
            }
        }

        #endregion
    }
}